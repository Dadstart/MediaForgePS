using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using PathResolverImpl = Dadstart.Labs.MediaForge.Services.System.PathResolver;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Fixes common OCR errors in SRT subtitle files (music note ♪ misreads, pipe as I, unmatched brackets, etc.).
/// </summary>
/// <remarks>
/// Can process a single file, multiple files, or a directory of .srt files. When processing a directory, all .srt files
/// are fixed in place. When processing a single file, use -OutputPath to write to a different file; otherwise the file is overwritten in place.
/// </remarks>
[Cmdlet(VerbsDiagnostic.Repair, "Subtitles")]
[OutputType(typeof(string))]
public class RepairSubtitlesCommand : CmdletBase
{
    /// <summary>
    /// Path to an SRT file or directory containing .srt files. Pipeline accepts path strings.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path to SRT file(s) or directory containing .srt files.")]
    [Alias("Path")]
    public string[] InputPath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Output path for the fixed SRT. Only used when a single file is processed; ignored for directories or multiple paths.
    /// </summary>
    [Parameter(Position = 1, HelpMessage = "Output path when processing a single file. Omit to overwrite in place.")]
    public string? OutputPath { get; set; }

    /// <summary>
    /// When input is a directory, recurse into subdirectories to find .srt files.
    /// </summary>
    [Parameter(HelpMessage = "When input is a directory, recurse into subdirectories.")]
    public SwitchParameter Recurse { get; set; }

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Directory structure under each input path is preserved.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy all files to before repairing; preserves directory structure.")]
    public string? BackupPath { get; set; }

    private readonly List<string> _inputPaths = new();
    private IPathResolver? _pathResolver;

    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    protected override void Begin()
    {
        Logger.LogDebug("Repair-Subtitles Begin");
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
        var writtenPaths = new List<string>();
        var totalPaths = resolvedPairs.Count;
        var pathIndex = 0;
        if (!PathResolverImpl.ResolveBackupPath(PathResolver, BackupPath, e => WriteError(e), out var resolvedBackupRoot))
            return;

        foreach (var pair in resolvedPairs)
        {
            pathIndex++;
            var (resolvedPath, isDirectory) = pair;
            var pathDisplayName = Path.GetFileName(resolvedPath) ?? resolvedPath;
            var mainPercent = totalPaths > 0 ? (int)((pathIndex * 100.0) / totalPaths) : 0;
            var mainStatus = $"Path {pathIndex} of {totalPaths} ({mainPercent}%) — {pathDisplayName}";
            MediaConversionHelper.WriteMainProgress(this, "Repairing subtitles", mainStatus, mainPercent, recordType: ProgressRecordType.Processing);

            if (isDirectory)
            {
                var files = Directory.EnumerateFiles(resolvedPath, "*.srt", searchOption).ToList();
                var fileCount = files.Count;
                var currentFileIndex = 0;
                foreach (var filePath in files)
                {
                    currentFileIndex++;
                    var filePercent = fileCount > 0 ? (int)((currentFileIndex * 100.0) / fileCount) : 0;
                    MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Repairing...", Path.GetFileName(filePath), percentComplete: filePercent, recordType: ProgressRecordType.Processing);
                    if (resolvedBackupRoot != null && !CopyToBackup(resolvedBackupRoot, resolvedPath, pathDisplayName, filePath, isDirectory))
                        continue;
                    if (ProcessFile(filePath, filePath))
                        writtenPaths.Add(filePath);
                    MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Completed", Path.GetFileName(filePath), percentComplete: filePercent, recordType: ProgressRecordType.Completed);
                }
            }
            else
            {
                MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Repairing...", pathDisplayName, percentComplete: 100, recordType: ProgressRecordType.Processing);
                if (resolvedBackupRoot == null || CopyToBackup(resolvedBackupRoot, resolvedPath, pathDisplayName, resolvedPath, isDirectory))
                {
                    var outputPath = resolvedPairs.Count == 1 && _inputPaths.Count == 1 && !string.IsNullOrWhiteSpace(OutputPath)
                        ? PathResolverImpl.ResolveOutputPathOrNull(PathResolver, OutputPath!, e => WriteError(e))
                        : resolvedPath;
                    if (outputPath != null && ProcessFile(resolvedPath, outputPath))
                        writtenPaths.Add(outputPath);
                }
                MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Completed", pathDisplayName, recordType: ProgressRecordType.Completed);
            }
        }

        MediaConversionHelper.WriteProgressCompleted(this, "Repairing subtitles", "Current file");

        foreach (var path in writtenPaths)
            WriteObject(path);
    }

    private bool CopyToBackup(string backupRoot, string resolvedPath, string pathDisplayName, string sourceFilePath, bool isDirectory)
    {
        try
        {
            var relativePath = isDirectory
                ? Path.Combine(pathDisplayName, Path.GetRelativePath(resolvedPath, sourceFilePath))
                : pathDisplayName;
            PathResolverImpl.CopyFileToBackup(backupRoot, sourceFilePath, relativePath);
            WriteVerbose($"Backed up to: {Path.Combine(backupRoot, relativePath)}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to copy to backup: {Source} -> {BackupPath}", sourceFilePath, backupRoot);
            WriteError(CreateErrorRecord(ex, "BackupFailed", ErrorCategory.WriteError, sourceFilePath));
            return false;
        }
    }

    private bool ProcessFile(string inputPath, string outputPath)
    {
        try
        {
            SrtOcrFixHelper.RepairFile(inputPath, outputPath);
            WriteVerbose($"Wrote fixed subtitles to: {outputPath}");
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
