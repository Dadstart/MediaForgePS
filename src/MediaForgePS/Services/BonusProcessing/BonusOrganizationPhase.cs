using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.BonusProcessing;

internal sealed class BonusOrganizationPhase(ILogger logger)
{
    public BonusOrganizationPhaseResult Run(
        ICmdletIO io,
        BonusOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(request.DestinationDirectory))
            throw new DirectoryNotFoundException($"Destination folder does not exist: '{request.DestinationDirectory}'");

        if (!Directory.Exists(request.SourceDirectory))
            throw new DirectoryNotFoundException($"Source folder does not exist: '{request.SourceDirectory}'");

        AddPlexFolders(request.DestinationDirectory);
        var filesMoved = MovePlexFiles(io, request.SourceDirectory, request.DestinationDirectory, cancellationToken);
        RemovePlexEmptyFolders(io, request.DestinationDirectory);

        return filesMoved;
    }

    private static void AddPlexFolders(string destinationDirectory)
    {
        foreach (var (folderName, _) in BonusPlexLayout._entries)
        {
            var path = Path.Combine(destinationDirectory, folderName);
            if (Directory.Exists(path))
                continue;

            Directory.CreateDirectory(path);
        }
    }

    private BonusOrganizationPhaseResult MovePlexFiles(
        ICmdletIO io,
        string sourceDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var filesMoved = 0;
        var moveCandidates = new List<(string SourceFile, string DestinationFolder, long FileSizeBytes)>();
        long totalBytes = 0;

        foreach (var (folderName, suffix) in BonusPlexLayout._entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destFolder = Path.Combine(destinationDirectory, folderName);
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            var videoPattern = $"*-{suffix}.mp4";

            var videoFiles = Directory.EnumerateFiles(sourceDirectory, videoPattern, SearchOption.AllDirectories);
            var subtitleFiles = BonusPlexLayout._subtitleExtensions
                .SelectMany(ext => Directory.EnumerateFiles(sourceDirectory, $"*-{suffix}.*{ext}", SearchOption.AllDirectories));
            var sourceFiles = videoFiles.Concat(subtitleFiles).ToList();

            if (sourceFiles.Count > 0)
                io.WriteVerbose($"Moving {sourceFiles.Count} files -{suffix} to {destFolder}");

            foreach (var sourceFile in sourceFiles)
            {
                var fileSizeBytes = GetFileSizeOrZero(sourceFile);
                totalBytes += fileSizeBytes;
                moveCandidates.Add((sourceFile, destFolder, fileSizeBytes));
            }
        }

        if (moveCandidates.Count == 0)
        {
            io.WriteWarning($"No bonus content files found to move in source directory {sourceDirectory}");
            return new BonusOrganizationPhaseResult(0, 0);
        }

        io.WriteVerbose($"Moving {moveCandidates.Count} Plex file(s) (total size: {MediaConversionHelper.FormatByteCount(totalBytes)})");

        long completedBytes = 0;
        var currentFileIndex = 0;
        foreach (var (sourceFile, destFolder, fileSizeBytes) in moveCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentFileIndex++;
            var fileName = Path.GetFileName(sourceFile);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                currentFileIndex,
                moveCandidates.Count,
                fileName,
                completedBytes,
                totalBytes);

            MediaConversionHelper.WriteMainProgress(io, "Plex file organization", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(io, "Current move file", $"Moving... - {fileName}", recordType: ProgressRecordType.Processing);

            var destinationPath = Path.Combine(destFolder, fileName);
            var currentFileStatus = "Completed";
            try
            {
                if (File.Exists(destinationPath))
                {
                    io.WriteWarning($"Destination file already exists, skipping: {destinationPath}");
                    currentFileStatus = "Skipped";
                }
                else
                {
                    io.WriteVerbose($"Moving {sourceFile} to {destFolder}");
                    var moveResult = PathHelper.MoveFile(sourceFile, destinationPath);
                    if (!moveResult.SourceRemoved && moveResult.SourceDeleteError is not null)
                    {
                        io.WriteWarning(
                            $"Copied '{sourceFile}' to '{destinationPath}' but could not remove the source file: {moveResult.SourceDeleteError}");
                    }

                    filesMoved++;
                }
            }
            catch (Exception ex)
            {
                currentFileStatus = "Failed";
                logger.LogWarning(
                    ex,
                    "Failed to move bonus file from {SourceFile} to {DestinationPath}",
                    sourceFile,
                    destinationPath);
                io.WriteError(new ErrorRecord(
                    ex,
                    "PlexMoveFailed",
                    ErrorCategory.WriteError,
                    sourceFile));
            }
            finally
            {
                completedBytes += fileSizeBytes;
                (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                    currentFileIndex,
                    moveCandidates.Count,
                    fileName,
                    completedBytes,
                    totalBytes);
                MediaConversionHelper.WriteMainProgress(io, "Plex file organization", status, percent, recordType: ProgressRecordType.Processing);
                MediaConversionHelper.WriteCurrentItemProgress(io, "Current move file", $"{currentFileStatus} - {fileName}", recordType: ProgressRecordType.Completed);
            }
        }

        MediaConversionHelper.WriteProgressCompleted(io, "Plex file organization", "Current move file");

        if (filesMoved == 0)
            io.WriteWarning($"No bonus content files found to move in source directory {sourceDirectory}");
        else
            io.WriteVerbose($"{filesMoved} files moved to Plex folders");

        return new BonusOrganizationPhaseResult(filesMoved, moveCandidates.Count);
    }

    private void RemovePlexEmptyFolders(ICmdletIO io, string destinationDirectory)
    {
        var foldersDeleted = 0;

        foreach (var (folderName, _) in BonusPlexLayout._entries)
        {
            var path = Path.Combine(destinationDirectory, folderName);
            if (!Directory.Exists(path))
                continue;

            if (Directory.EnumerateFileSystemEntries(path).Any())
                continue;

            try
            {
                io.WriteVerbose($"Removing empty Plex folder: {path}");
                Directory.Delete(path);
                foldersDeleted++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove empty Plex folder: {FolderPath}", path);
                io.WriteError(new ErrorRecord(
                    ex,
                    "PlexFolderRemovalFailed",
                    ErrorCategory.WriteError,
                    path));
            }
        }

        if (foldersDeleted == 0)
            io.WriteWarning($"No empty Plex folders found to remove in '{destinationDirectory}'");
        else
            io.WriteVerbose($"{foldersDeleted} empty Plex folders deleted");
    }

    internal static long GetFileSizeOrZero(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            return fileInfo.Exists ? fileInfo.Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}
