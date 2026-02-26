using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Exports English subtitle streams from media files. With -Ocr, converts image-based formats (SUP, SUB) to SRT via OCR and optionally repairs SRT files.
/// </summary>
/// <remarks>
/// Always extracts subtitle tracks matching English language. With -Ocr: converts .sup/.sub to SRT (requires Subtitle Edit and Tesseract), then repairs SRT files unless -NoRepair is specified.
/// Output SRT paths are written to the pipeline when -Ocr is used (and for repaired/native SRT when applicable).
/// </remarks>
[Cmdlet(VerbsData.Export, "Subtitles")]
[Alias("Export-RepairedSubtitles")]
[OutputType(typeof(string))]
public class ExportSubtitlesCommand : CmdletBase
{
    /// <summary>
    /// Media file path(s) or folder path(s). For folders, all .mkv files are processed. Pipeline accepts path strings or MediaFile objects from Get-MediaFile.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path(s) to media file(s) or folder(s) containing .mkv files.")]
    [Alias("Path")]
    public object[]? InputPath { get; set; }

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Directory structure is preserved under this root. Only used when -Ocr is specified and repair runs.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy SRT files to before repairing; preserves path structure.")]
    public string? BackupPath { get; set; }

    /// <summary>
    /// Maximum number of image-to-SRT conversions to run in parallel. Default is 10. Only applies when -Ocr is specified.
    /// </summary>
    [Parameter(HelpMessage = "Maximum number of image subtitle conversions to run simultaneously.")]
    public int ThrottleLimit { get; set; } = 10;

    /// <summary>
    /// When specified, converts image-based subtitles (SUP, SUB) to SRT via OCR and repairs SRT files (unless -NoRepair is also specified).
    /// </summary>
    [Parameter(HelpMessage = "Convert image subtitles to SRT via OCR and repair SRT files.")]
    public SwitchParameter Ocr { get; set; }

    /// <summary>
    /// When specified with -Ocr, skips the SRT repair step. Has no effect without -Ocr.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair when used with -Ocr.")]
    public SwitchParameter NoRepair { get; set; }

    private readonly List<object> _pathOrMediaFiles = new();
    private IMediaReaderService? _mediaReaderService;
    private IExecutableService? _executableService;
    private IPathResolver? _pathResolver;

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();
    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
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

        var mediaFiles = SubtitleExportHelper.ResolveMediaFiles(_pathOrMediaFiles, this, MediaReaderService, Logger, e => WriteError(e)).ToList();
        if (mediaFiles.Count == 0)
        {
            WriteWarning("No media files to process.");
            return;
        }

        var filesWithSize = new List<(MediaFile Mf, long Size)>();
        long totalBytes = 0;
        foreach (var mf in mediaFiles)
        {
            long size = 0;
            try
            {
                var fi = new FileInfo(mf.Path);
                if (fi.Exists)
                    size = fi.Length;
                totalBytes += size;
            }
            catch
            {
                // Use 0 for this file
            }
            filesWithSize.Add((mf, size));
        }

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
            MediaConversionHelper.WriteMainProgress(this, "Exporting subtitles", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Exporting...", fileName, recordType: ProgressRecordType.Processing);

            ExportSubtitlesForMediaFile(mf, totalFiles, fileIndex, exportedPaths);

            completedBytes += fileSize;
            (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(fileIndex, totalFiles, fileName, completedBytes, totalBytes);
            MediaConversionHelper.WriteMainProgress(this, "Exporting subtitles", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Completed", fileName, recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(this, "Exporting subtitles", "Current file");

        if (exportedPaths.Count == 0)
        {
            WriteHostMessage("No subtitle files exported.", ConsoleColor.Green);
            return;
        }

        var imagePaths = SubtitlePathHelper.GetImageSubtitlePaths(exportedPaths);
        var srtPathsFromExport = SubtitlePathHelper.GetSrtPaths(exportedPaths);

        IReadOnlyList<string> convertedSrtPaths = Array.Empty<string>();
        if (Ocr.IsPresent && imagePaths.Count > 0)
        {
            var subtitleEditPath = WindowsExecutablePathHelper.GetSubtitleEditPath();
            if (string.IsNullOrEmpty(subtitleEditPath))
            {
                WriteError(CreateErrorRecord(
                    new FileNotFoundException($"Subtitle Edit not found (required to convert {imagePaths.Count} image subtitle(s)). Expected: {WindowsExecutablePathHelper.GetSubtitleEditExpectedPath()}"),
                    "SubtitleEditNotFound",
                    ErrorCategory.ObjectNotFound,
                    null));
                return;
            }
            _ = ExecutableService;
            convertedSrtPaths = ImageSubtitleConversionHelper.ConvertImagePathsToSrtParallel(
                this, ExecutableService, Logger, subtitleEditPath, imagePaths, Math.Max(1, ThrottleLimit), WriteError);
        }

        var allSrtPaths = srtPathsFromExport.Concat(convertedSrtPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (allSrtPaths.Count == 0)
        {
            WriteHostMessage("No SRT files to repair (only non-SRT formats were exported).", ConsoleColor.Green);
            return;
        }

        var shouldRepair = Ocr.IsPresent && !NoRepair.IsPresent;
        SrtRepairHelper.RunRepairLoop(this, Logger, PathResolver, allSrtPaths, shouldRepair, BackupPath, WriteObject);

        WriteHostMessage("Export completed.", ConsoleColor.Green);
    }

    private void ExportSubtitlesForMediaFile(MediaFile mediaFile, int totalFiles, int fileIndex, List<string> exportedPaths)
    {
        var fileName = Path.GetFileNameWithoutExtension(mediaFile.Path);
        WriteVerbose($"[{fileIndex}/{totalFiles}] Processing: {fileName}");

        var subtitles = (mediaFile.Streams ?? Array.Empty<MediaStream>())
            .Where(s => string.Equals(s.Type, "subtitle", StringComparison.OrdinalIgnoreCase) &&
                (s.Language ?? "").StartsWith("en", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (subtitles.Count == 0)
        {
            WriteVerbose($"No English subtitles in {fileName}");
            return;
        }

        var subIndex = 0;
        foreach (var sub in subtitles)
        {
            subIndex++;
            var percent = (int)Math.Round((subIndex * 100.0) / subtitles.Count, 0);
            MediaConversionHelper.WriteCurrentItemProgress(this, fileName, $"Stream {sub.Index} ({sub.Codec})", percentComplete: percent);
            if (ExportSingleSubtitle(sub, mediaFile, subtitles.Count, out var path))
                exportedPaths.Add(path);
        }
        MediaConversionHelper.WriteCurrentItemProgress(this, fileName, "Complete", recordType: ProgressRecordType.Completed);
    }

    private bool ExportSingleSubtitle(MediaStream stream, MediaFile mediaFile, int totalSubtitleCount, out string resolvedOutput)
    {
        resolvedOutput = string.Empty;
        if (!SubtitleExportHelper.CodecToExtension.TryGetValue(stream.Codec ?? "", out var ext))
        {
            WriteWarning($"Unknown codec: {stream.Codec} - using .bin extension");
            ext = "bin";
        }

        var newPath = SubtitleExportHelper.GetOutputPath(mediaFile.Path, stream.Index, totalSubtitleCount, ext);
        if (!PathResolver.TryResolveOutputPath(newPath, out var resolved))
        {
            WriteError(new ErrorRecord(new InvalidOperationException($"Failed to resolve output path: {newPath}"), "OutputPathFailed", ErrorCategory.InvalidArgument, newPath));
            return false;
        }
        resolvedOutput = resolved;

        try
        {
            SubtitleExportHelper.ExtractSubtitle(
                ExecutableService,
                stream,
                mediaFile.Path,
                resolved,
                WindowsExecutablePathHelper.GetMkvextractPath());
            WriteVerbose($"Extracted {Path.GetFileName(resolved)}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract subtitle stream {Index} from {Path}", stream.Index, mediaFile.Path);
            WriteError(new ErrorRecord(ex, "SubtitleExportFailed", ErrorCategory.OperationStopped, mediaFile.Path));
            return false;
        }
    }

}
