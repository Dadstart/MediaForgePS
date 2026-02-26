using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using PathResolverImpl = Dadstart.Labs.MediaForge.Services.System.PathResolver;

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

        var imagePaths = exportedPaths
            .Where(p =>
            {
                var ext = Path.GetExtension(p);
                return ext.Equals(".sup", StringComparison.OrdinalIgnoreCase) || ext.Equals(".sub", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var srtPathsFromExport = exportedPaths
            .Where(p => Path.GetExtension(p).Equals(".srt", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var convertedSrtPaths = new ConcurrentBag<string>();
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

            // Ensure services are created on the pipeline thread before running parallel work.
            _ = ExecutableService;
            var totalConvert = imagePaths.Count;
            var errors = new ConcurrentBag<(string InputPath, Exception Exception)>();
            var completedCount = 0;

            MediaConversionHelper.WriteMainProgress(
                this,
                "Converting image subtitles to SRT",
                $"Converting {totalConvert} image subtitle file(s) to SRT...",
                0,
                recordType: ProgressRecordType.Processing);

            var maxParallel = Math.Max(1, ThrottleLimit);
            using var throttle = new SemaphoreSlim(maxParallel, maxParallel);
            var tasks = imagePaths
                .Select(inputPath => Task.Run(() =>
                {
                    throttle.Wait();
                    try
                    {
                        var srtPath = Path.ChangeExtension(inputPath, "srt") ?? inputPath + ".srt";
                        try
                        {
                            ImageSubtitleConversionHelper.ConvertToSrt(ExecutableService, subtitleEditPath, inputPath, srtPath);
                            Logger.LogDebug("Converted image subtitles to SRT: {Path}", srtPath);
                            convertedSrtPaths.Add(srtPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Failed to convert image subtitles to SRT: {Path}", inputPath);
                            errors.Add((inputPath, ex));
                        }
                        finally
                        {
                            Interlocked.Increment(ref completedCount);
                        }
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }))
                .ToArray();

            while (Volatile.Read(ref completedCount) < totalConvert)
            {
                var current = Volatile.Read(ref completedCount);
                var percent = totalConvert > 0 ? (int)((current * 100.0) / totalConvert) : 0;

                MediaConversionHelper.WriteMainProgress(
                    this,
                    "Converting image subtitles to SRT",
                    $"Converted {current} of {totalConvert} image subtitle file(s) to SRT...",
                    percent,
                    recordType: ProgressRecordType.Processing);

                Thread.Sleep(200);
            }

            Task.WaitAll(tasks);

            MediaConversionHelper.WriteMainProgress(
                this,
                "Converting image subtitles to SRT",
                $"Converted {totalConvert} of {totalConvert} image subtitle file(s) to SRT...",
                100,
                recordType: ProgressRecordType.Processing);

            foreach (var error in errors)
                WriteError(CreateErrorRecord(error.Exception, "ConvertImageSubtitlesToSrtFailed", ErrorCategory.OperationStopped, error.InputPath));

            MediaConversionHelper.WriteProgressCompleted(this, "Converting image subtitles to SRT", "Current file");
        }

        var allSrtPaths = srtPathsFromExport.Concat(convertedSrtPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (allSrtPaths.Count == 0)
        {
            WriteHostMessage("No SRT files to repair (only non-SRT formats were exported).", ConsoleColor.Green);
            return;
        }

        var shouldRepair = Ocr.IsPresent && !NoRepair.IsPresent;
        string? resolvedBackupRoot = null;
        if (shouldRepair && !PathResolverImpl.ResolveBackupPath(PathResolver, BackupPath, e => WriteError(e), out resolvedBackupRoot))
            return;
        var totalRepair = allSrtPaths.Count;
        for (var idx = 0; idx < allSrtPaths.Count; idx++)
        {
            var srtPath = allSrtPaths[idx];
            var displayName = Path.GetFileName(srtPath);
            var pct = totalRepair > 0 ? (int)(((idx + 1) * 100.0) / totalRepair) : 0;
            MediaConversionHelper.WriteMainProgress(this, "Repairing subtitles", $"File {idx + 1} of {totalRepair} — {displayName}", pct, recordType: ProgressRecordType.Processing);
            if (shouldRepair)
            {
                if (resolvedBackupRoot != null && !CopyToBackup(resolvedBackupRoot, srtPath))
                    continue;
                if (RepairSrtFile(srtPath, srtPath))
                    WriteObject(srtPath);
            }
            else
                WriteObject(srtPath);
        }
        MediaConversionHelper.WriteProgressCompleted(this, "Repairing subtitles", "Current file");

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

    private bool CopyToBackup(string backupRoot, string sourceFilePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(sourceFilePath);
            var pathRoot = Path.GetPathRoot(fullPath);
            var relative = string.IsNullOrEmpty(pathRoot) ? Path.GetFileName(sourceFilePath) : Path.GetRelativePath(pathRoot, fullPath).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            PathResolverImpl.CopyFileToBackup(backupRoot, sourceFilePath, relative);
            WriteVerbose($"Backed up to: {Path.Combine(backupRoot, relative)}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to copy to backup: {Source} -> {BackupPath}", sourceFilePath, backupRoot);
            WriteError(CreateErrorRecord(ex, "BackupFailed", ErrorCategory.WriteError, sourceFilePath));
            return false;
        }
    }

    private bool RepairSrtFile(string inputPath, string outputPath)
    {
        try
        {
            SrtOcrFixHelper.RepairFile(inputPath, outputPath);
            WriteVerbose($"Repaired: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to repair subtitles: {Path}", inputPath);
            WriteError(CreateErrorRecord(ex, "RepairSubtitlesFailed", ErrorCategory.WriteError, inputPath));
            return false;
        }
    }
}
