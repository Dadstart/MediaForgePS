using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Exports English subtitle streams from media files.
/// </summary>
/// <remarks>
/// Always extracts subtitle tracks whose language matches English. Folder input processes <c>*.mkv</c> files only.
/// Use -Ocr Auto (default), Skip, or Force to control OCR of image-based subtitles after extraction.
/// When OCR runs, only OCR-produced SRT files are repaired; native exported SRT files are not repaired.
/// Writes a <see cref="SubtitleProcessingResult"/> with extract and OCR conversion counts.
/// Alias: Export-RepairedSubtitles.
/// Supports -WhatIf and -Confirm.
/// </remarks>
[Cmdlet(VerbsData.Export, "Subtitles", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[Alias("Export-RepairedSubtitles")]
[OutputType(typeof(SubtitleProcessingResult))]
public class ExportSubtitlesCommand : ProgressCmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    /// <summary>
    /// Media file path(s) or folder path(s). For folders, all .mkv files are processed. Pipeline accepts path strings or MediaFile objects from Get-MediaFile.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path(s) to media file(s) or folder(s) containing .mkv files.")]
    [Alias("Path")]
    public object[]? InputPath { get; set; }

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Directory structure is preserved under this root. Only used when OCR processing runs and repair is enabled.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy SRT files to before repairing; preserves path structure.")]
    public string? BackupPath { get; set; }

    /// <summary>
    /// Maximum number of image-to-SRT conversions to run in parallel. Default is 10. Only applies when -Ocr is Force or Auto.
    /// </summary>
    [Parameter(HelpMessage = "Maximum number of image subtitle conversions to run simultaneously.")]
    public int ThrottleLimit { get; set; } = 10;

    /// <summary>
    /// Controls OCR of image-based subtitles (SUP, SUB). Default is Auto. Skip leaves exported subtitles unchanged; Force OCRs all image subtitle files; Auto OCRs image subtitles when the source has a single exported subtitle format and it is not SRT.
    /// </summary>
    [Parameter(HelpMessage = "OCR mode for image subtitles: Auto, Skip, or Force.")]
    [ValidateSet(SubtitleOcrMode.Auto, SubtitleOcrMode.Skip, SubtitleOcrMode.Force, IgnoreCase = true)]
    public string Ocr { get; set; } = SubtitleOcrMode.Default;

    /// <summary>
    /// When specified, skips repair of OCR-produced SRT files. Has no effect when -Ocr is Skip.
    /// </summary>
    [Parameter(HelpMessage = "Skip repair of OCR-produced SRT files.")]
    public SwitchParameter SkipRepair { get; set; }

    /// <summary>
    /// Keeps source .sup/.sub/.idx files after successful OCR conversion, and keeps unused image sidecars Auto would otherwise discard when a text SRT is already present. By default they are deleted.
    /// </summary>
    [Parameter(HelpMessage = "Keep source image subtitle files after successful OCR conversion.")]
    public SwitchParameter KeepSource { get; set; }

    private readonly List<object> _pathOrMediaFiles = new();
    private IMediaReaderService? _mediaReaderService;
    private IExecutableService? _executableService;
    private IImageSubtitleOcrConverter? _ocrConverter;
    private IPathResolver? _pathResolver;

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();
    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
    private IImageSubtitleOcrConverter OcrConverter => _ocrConverter ??= ModuleServices.GetRequiredService<IImageSubtitleOcrConverter>();
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    protected override void Begin()
    {
        Logger.LogDebug("Export-Subtitles Begin");
    }

    protected override void Process()
    {
        if (InputPath == null || InputPath.Length == 0)
            return;
        foreach (var p in InputPath)
        {
            if (p != null)
                _pathOrMediaFiles.Add(p);
        }
    }

    protected override void End()
    {
        if (_pathOrMediaFiles.Count == 0)
        {
            WriteWarning("No paths or media files provided.");
            return;
        }

        var mediaFiles = SubtitleExportHelper.ResolveMediaFiles(_pathOrMediaFiles, CmdletIO.Paths, MediaReaderService, Logger, e => WriteError(e), StoppingToken).ToList();
        if (mediaFiles.Count == 0)
        {
            WriteWarning("No media files to process.");
            return;
        }

        var filesWithSize = MediaConversionHelper.BuildItemsWithSizes(mediaFiles, mf => mf.Path, out var totalBytes)
            .Select(entry => (Mf: entry.Item, entry.Size))
            .ToList();

        WriteHostMessage($"Exporting subtitles from {mediaFiles.Count} file(s) (total size: {MediaConversionHelper.FormatByteCount(totalBytes)})", ConsoleColor.Cyan);

        var exportedPaths = new List<string>();
        long completedBytes = 0;
        var totalFiles = filesWithSize.Count;
        for (var i = 0; i < filesWithSize.Count; i++)
        {
            var (mf, fileSize) = filesWithSize[i];
            var fileIndex = i + 1;
            var fileName = Path.GetFileName(mf.Path);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(fileIndex, totalFiles, fileName, completedBytes, totalBytes);
            MediaConversionHelper.WriteMainProgress(CmdletIO, "Exporting subtitles", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "Current file", "Exporting...", fileName, recordType: ProgressRecordType.Processing);

            if (ShouldProcess(mf.Path, $"Export subtitles from '{fileName}'"))
                ExportSubtitlesForMediaFile(mf, totalFiles, fileIndex, exportedPaths);

            completedBytes += fileSize;
            (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(fileIndex, totalFiles, fileName, completedBytes, totalBytes);
            MediaConversionHelper.WriteMainProgress(CmdletIO, "Exporting subtitles", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "Current file", "Completed", fileName, recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(CmdletIO, "Exporting subtitles", "Current file");

        if (exportedPaths.Count == 0)
        {
            WriteHostMessage("No subtitle files exported.", ConsoleColor.Green);
            WriteObject(SubtitleProcessingResult.Empty);
            return;
        }

        IReadOnlyList<string> convertedPaths = Array.Empty<string>();
        if (SubtitleOcrMode.RequiresOcrProcessing(Ocr))
        {
            var imagePaths = SubtitlePathHelper.SelectImagePathsForOcr(exportedPaths, Ocr);
            if (imagePaths.Count > 0)
            {
                var srtPathsFromExport = SubtitlePathHelper.GetSrtPaths(exportedPaths);
                var ocrResult = SubtitleOcrRepairWorkflow.Run(
                    CmdletIO,
                    Logger,
                    OcrConverter,
                    PathResolver,
                    imagePaths,
                    srtPathsFromExport,
                    performOcr: true,
                    ThrottleLimit,
                    shouldRepair: SubtitleOcrMode.ShouldRepair(Ocr, SkipRepair.IsPresent),
                    BackupPath,
                    StoppingToken,
                    KeepSource.IsPresent);

                if (ocrResult == null)
                {
                    WriteSubtitleProcessingResult(exportedPaths, convertedPaths);
                    return;
                }

                convertedPaths = ocrResult.ConvertedSrtPaths;
            }

            ImageSubtitleConversionHelper.DeleteUnusedImageSubtitleSources(
                exportedPaths,
                Ocr,
                KeepSource.IsPresent,
                Logger);
        }

        WriteSubtitleProcessingResult(exportedPaths, convertedPaths);
    }

    private void WriteSubtitleProcessingResult(IReadOnlyList<string> extractedPaths, IReadOnlyList<string> convertedPaths)
    {
        var result = SubtitleProcessingResult.Create(extractedPaths, convertedPaths);
        WriteHostMessage(
            $"Export completed: {result.ExtractedCount} extracted, {result.ConvertedCount} converted.",
            ConsoleColor.Green);
        WriteObject(result);
    }

    private void ExportSubtitlesForMediaFile(MediaFile mediaFile, int totalFiles, int fileIndex, List<string> exportedPaths)
    {
        var fileName = Path.GetFileName(mediaFile.Path);
        WriteVerbose($"[{fileIndex}/{totalFiles}] Processing: {fileName}");

        var mkvextractPath = WindowsExecutablePathHelper.GetMkvextractPath();
        var subtitles = SubtitleExportHelper.GetEnglishSubtitleStreams(mediaFile);
        var subIndex = 0;

        var extracted = SubtitleExportHelper.ExtractEnglishSubtitles(
            ExecutableService,
            mediaFile,
            mkvextractPath,
            buildOutputPath: plan =>
            {
                subIndex++;
                var percent = (int)Math.Round((subIndex * 100.0) / subtitles.Count, 0);
                MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, fileName, $"Stream {plan.Stream.Index} ({plan.Stream.Codec})", percentComplete: percent);
                return SubtitleExportHelper.GetOutputPath(
                    mediaFile.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount);
            },
            finalizeOutputPath: candidate => TryResolveOutputPath(PathResolver, candidate, out var resolved) ? resolved : null,
            onUnknownCodec: stream => WriteWarning($"Unknown codec: {stream.Codec} - using .bin extension"),
            onExtractFailed: (_, ex) =>
            {
                if (ex is PlatformNotSupportedException pns)
                {
                    WriteWarning(pns.Message);
                    return;
                }

                WriteStandardError(ex, ErrorIds.SubtitleExportFailed, ErrorCategory.OperationStopped, mediaFile.Path);
            },
            onNoEnglishSubtitles: () => WriteVerbose($"No English subtitles in {fileName}"),
            Logger,
            StoppingToken);

        foreach (var path in extracted)
        {
            WriteVerbose($"Extracted {Path.GetFileName(path)}");
            exportedPaths.Add(path);
        }

        MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, fileName, "Complete", recordType: ProgressRecordType.Completed);
    }
}
