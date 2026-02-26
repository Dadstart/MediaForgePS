using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using PathResolverImpl = Dadstart.Labs.MediaForge.Services.System.PathResolver;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts image-based subtitle files (SUP, SUB) to SRT via OCR, then repairs all SRT files (including any SRT files in the input list). For use when subtitles are already extracted to disk.
/// </summary>
/// <remarks>
/// Equivalent to running Convert-ImageSubtitlesToSrt on SUP/SUB paths then Repair-Subtitles on all SRT paths. Requires Subtitle Edit (and Tesseract) when any input is SUP or SUB. Output SRT paths are written to the pipeline.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "SubtitleOcrRepair")]
[OutputType(typeof(string))]
public class InvokeSubtitleOcrRepairCommand : CmdletBase
{
    /// <summary>
    /// Path(s) to .sup, .sub, or .srt file(s), or directory/directories containing them. Pipeline accepts path strings.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path(s) to SUP, SUB, or SRT file(s) or directory/directories containing them.")]
    [Alias("Path")]
    public string[] InputPath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Directory structure is preserved under this root.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy SRT files to before repairing; preserves path structure.")]
    public string? BackupPath { get; set; }

    /// <summary>
    /// Maximum number of image-to-SRT conversions to run in parallel. Default is 10.
    /// </summary>
    [Parameter(HelpMessage = "Maximum number of image subtitle conversions to run simultaneously.")]
    public int ThrottleLimit { get; set; } = 10;

    /// <summary>
    /// When specified, skips the SRT repair step. Only conversion (SUP/SUB to SRT) is performed; converted and existing SRT paths are still written to the pipeline.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair; only convert image subtitles to SRT.")]
    public SwitchParameter NoRepair { get; set; }

    /// <summary>
    /// When input is a directory, recurse into subdirectories to find .sup, .sub, and .srt files.
    /// </summary>
    [Parameter(HelpMessage = "When input is a directory, recurse into subdirectories.")]
    public SwitchParameter Recurse { get; set; }

    private readonly List<string> _inputPaths = new();
    private IExecutableService? _executableService;
    private IPathResolver? _pathResolver;

    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    protected override void Begin()
    {
        Logger.LogDebug("Invoke-SubtitleOcrRepair Begin");
    }

    protected override void Process()
    {
        if (InputPath == null || InputPath.Length == 0)
            return;
        foreach (var p in InputPath)
        {
            if (!string.IsNullOrWhiteSpace(p))
                _inputPaths.Add(p.Trim());
        }
    }

    protected override void End()
    {
        if (_inputPaths.Count == 0)
        {
            WriteWarning("No input path(s) provided.");
            return;
        }

        var resolvedPairs = PathResolverImpl.ResolveFileOrDirectoryPaths(this, _inputPaths, Logger, e => WriteError(e));
        if (resolvedPairs.Count == 0)
        {
            WriteWarning("No existing file or directory paths could be resolved.");
            return;
        }

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var allSubtitlePaths = new List<string>();
        foreach (var (resolvedPath, isDirectory) in resolvedPairs)
        {
            if (isDirectory)
            {
                var files = Directory
                    .EnumerateFiles(resolvedPath, "*.*", searchOption)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f);
                        return ext.Equals(".sup", StringComparison.OrdinalIgnoreCase)
                            || ext.Equals(".sub", StringComparison.OrdinalIgnoreCase)
                            || ext.Equals(".srt", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
                allSubtitlePaths.AddRange(files);
            }
            else
            {
                var ext = Path.GetExtension(resolvedPath);
                if (ext.Equals(".sup", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".sub", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".srt", StringComparison.OrdinalIgnoreCase))
                    allSubtitlePaths.Add(resolvedPath);
            }
        }

        var imagePaths = allSubtitlePaths
            .Where(p =>
            {
                var ext = Path.GetExtension(p);
                return ext.Equals(".sup", StringComparison.OrdinalIgnoreCase) || ext.Equals(".sub", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var srtPathsFromInput = allSubtitlePaths
            .Where(p => Path.GetExtension(p).Equals(".srt", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (imagePaths.Count == 0 && srtPathsFromInput.Count == 0)
        {
            WriteWarning("No SUP, SUB, or SRT files found at the given path(s).");
            return;
        }

        WriteHostMessage($"Processing {imagePaths.Count} image subtitle(s) and {srtPathsFromInput.Count} SRT file(s).", ConsoleColor.Cyan);

        var convertedSrtPaths = new ConcurrentBag<string>();
        if (imagePaths.Count > 0)
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

        var allSrtPaths = srtPathsFromInput.Concat(convertedSrtPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (allSrtPaths.Count == 0)
        {
            WriteHostMessage("No SRT files to repair.", ConsoleColor.Green);
            return;
        }

        var shouldRepair = !NoRepair.IsPresent;
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

        WriteHostMessage("OCR and repair completed.", ConsoleColor.Green);
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
