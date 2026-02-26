using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Represents one repair operation input/output pair, including optional backup relative path.
    /// </summary>
    public readonly record struct SrtRepairItem(string InputPath, string OutputPath, string? BackupRelativePath = null);

    /// <summary>
    /// Copies a file to backup using an explicit relative path. Reports via cmdlet.
    /// </summary>
    public static bool CopyToBackupWithRelativePath(
        PSCmdlet cmdlet,
        ILogger logger,
        string backupRoot,
        string sourceFilePath,
        string relativePath)
    {
        try
        {
            PathResolverImpl.CopyFileToBackup(backupRoot, sourceFilePath, relativePath);
            cmdlet.WriteVerbose($"Backed up to: {Path.Combine(backupRoot, relativePath)}");
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
    /// Copies a file to backup using a relative path from the path root (flat structure under backupRoot). Reports via cmdlet.
    /// </summary>
    public static bool CopyToBackupFromPathRoot(
        PSCmdlet cmdlet,
        ILogger logger,
        string backupRoot,
        string sourceFilePath)
    {
        var fullPath = Path.GetFullPath(sourceFilePath);
        var pathRoot = Path.GetPathRoot(fullPath);
        var relative = string.IsNullOrEmpty(pathRoot)
            ? Path.GetFileName(sourceFilePath)
            : Path.GetRelativePath(pathRoot, fullPath).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return CopyToBackupWithRelativePath(cmdlet, logger, backupRoot, sourceFilePath, relative);
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
        var repairItems = srtPaths
            .Select(path => new SrtRepairItem(path, path))
            .ToList();

        RunRepairLoop(cmdlet, logger, pathResolver, repairItems, shouldRepair, backupPath, writeObject);
    }

    /// <summary>
    /// Runs the repair loop using explicit input/output repair items.
    /// </summary>
    public static void RunRepairLoop(
        PSCmdlet cmdlet,
        ILogger logger,
        IPathResolver pathResolver,
        IReadOnlyList<SrtRepairItem> repairItems,
        bool shouldRepair,
        string? backupPath,
        Action<string> writeObject)
    {
        if (repairItems.Count == 0)
            return;
        string? resolvedBackupRoot = null;
        if (shouldRepair && !PathResolverImpl.ResolveBackupPath(pathResolver, backupPath, er => cmdlet.WriteError(er), out resolvedBackupRoot))
            return;
        var totalRepair = repairItems.Count;
        for (var idx = 0; idx < repairItems.Count; idx++)
        {
            var repairItem = repairItems[idx];
            var displayName = Path.GetFileName(repairItem.InputPath);
            var pct = totalRepair > 0 ? (int)(((idx + 1) * 100.0) / totalRepair) : 0;
            MediaConversionHelper.WriteMainProgress(cmdlet, "Repairing subtitles", $"File {idx + 1} of {totalRepair} — {displayName}", pct, recordType: ProgressRecordType.Processing);
            if (shouldRepair)
            {
                if (resolvedBackupRoot != null)
                {
                    var copied = string.IsNullOrWhiteSpace(repairItem.BackupRelativePath)
                        ? CopyToBackupFromPathRoot(cmdlet, logger, resolvedBackupRoot, repairItem.InputPath)
                        : CopyToBackupWithRelativePath(cmdlet, logger, resolvedBackupRoot, repairItem.InputPath, repairItem.BackupRelativePath!);
                    if (!copied)
                        continue;
                }

                if (RepairFileWithReporting(cmdlet, logger, repairItem.InputPath, repairItem.OutputPath))
                    writeObject(repairItem.OutputPath);
            }
            else
                writeObject(repairItem.OutputPath);
        }
        MediaConversionHelper.WriteProgressCompleted(cmdlet, "Repairing subtitles", "Current file");
    }
}
