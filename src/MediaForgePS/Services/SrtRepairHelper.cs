using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using PathResolverImpl = Dadstart.Labs.MediaForge.Services.System.PathResolver;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Shared logic for backing up and repairing SRT files (used by Export-Subtitles and Invoke-SubtitleOcrRepair).
/// </summary>
public static class SrtRepairHelper
{
    /// <summary>
    /// Copies a file to backup using a relative path from the path root (flat structure under backupRoot). Reports via cmdlet.
    /// </summary>
    public static bool CopyToBackupFromPathRoot(
        PSCmdlet cmdlet,
        ILogger logger,
        string backupRoot,
        string sourceFilePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(sourceFilePath);
            var pathRoot = Path.GetPathRoot(fullPath);
            var relative = string.IsNullOrEmpty(pathRoot)
                ? Path.GetFileName(sourceFilePath)
                : Path.GetRelativePath(pathRoot, fullPath).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            PathResolverImpl.CopyFileToBackup(backupRoot, sourceFilePath, relative);
            cmdlet.WriteVerbose($"Backed up to: {Path.Combine(backupRoot, relative)}");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to copy to backup: {Source} -> {BackupPath}", sourceFilePath, backupRoot);
            cmdlet.WriteError(new ErrorRecord(ex, "BackupFailed", ErrorCategory.WriteError, sourceFilePath));
            return false;
        }
    }

    /// <summary>
    /// Repairs an SRT file in place (or to outputPath). Reports success/failure via cmdlet; returns false on exception.
    /// </summary>
    public static bool RepairFileWithReporting(
        PSCmdlet cmdlet,
        ILogger logger,
        string inputPath,
        string outputPath)
    {
        try
        {
            SrtOcrFixHelper.RepairFile(inputPath, outputPath);
            cmdlet.WriteVerbose($"Repaired: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to repair subtitles: {Path}", inputPath);
            cmdlet.WriteError(new ErrorRecord(ex, "RepairSubtitlesFailed", ErrorCategory.WriteError, inputPath));
            return false;
        }
    }

    /// <summary>
    /// Runs the repair loop: optionally resolve backup path, then for each SRT path optionally backup and repair, and write path to pipeline on success.
    /// </summary>
    public static void RunRepairLoop(
        PSCmdlet cmdlet,
        ILogger logger,
        IPathResolver pathResolver,
        IReadOnlyList<string> srtPaths,
        bool shouldRepair,
        string? backupPath,
        Action<string> writeObject)
    {
        if (srtPaths.Count == 0)
            return;
        string? resolvedBackupRoot = null;
        if (shouldRepair && !PathResolverImpl.ResolveBackupPath(pathResolver, backupPath, er => cmdlet.WriteError(er), out resolvedBackupRoot))
            return;
        var totalRepair = srtPaths.Count;
        for (var idx = 0; idx < srtPaths.Count; idx++)
        {
            var srtPath = srtPaths[idx];
            var displayName = Path.GetFileName(srtPath);
            var pct = totalRepair > 0 ? (int)(((idx + 1) * 100.0) / totalRepair) : 0;
            MediaConversionHelper.WriteMainProgress(cmdlet, "Repairing subtitles", $"File {idx + 1} of {totalRepair} — {displayName}", pct, recordType: ProgressRecordType.Processing);
            if (shouldRepair)
            {
                if (resolvedBackupRoot != null && !CopyToBackupFromPathRoot(cmdlet, logger, resolvedBackupRoot, srtPath))
                    continue;
                if (RepairFileWithReporting(cmdlet, logger, srtPath, srtPath))
                    writeObject(srtPath);
            }
            else
                writeObject(srtPath);
        }
        MediaConversionHelper.WriteProgressCompleted(cmdlet, "Repairing subtitles", "Current file");
    }
}
