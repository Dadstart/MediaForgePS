using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

internal sealed class SeriesVideoCopyPhase
{
    public IReadOnlyList<string> GetFilteredVideoFiles(
        PSCmdlet cmdlet,
        IReadOnlyList<string> paths,
        IReadOnlyList<string> filePatterns,
        long minimumFileSizeBytes)
    {
        var resolvedDirectories = ResolveDirectories(cmdlet, paths);
        if (resolvedDirectories.Count == 0)
            return Array.Empty<string>();

        var acceptedFiles = new List<string>();
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in resolvedDirectories)
        {
            foreach (var pattern in filePatterns)
            {
                foreach (var file in Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly))
                {
                    if (!seenFiles.Add(file))
                        continue;

                    if (new FileInfo(file).Length > minimumFileSizeBytes)
                        acceptedFiles.Add(file);
                }
            }
        }

        return acceptedFiles;
    }

    public IReadOnlyList<string> CopyVideoFilesWithMetadata(
        PSCmdlet cmdlet,
        VideoCopyRequest request,
        Func<string, int, TvDbEpisodeInfo, string, string> buildEpisodeFileName)
    {
        var acceptedFiles = GetFilteredVideoFiles(cmdlet, request.Paths, request.FilePatterns, request.MinimumFileSizeBytes);
        if (acceptedFiles.Count == 0)
            return Array.Empty<string>();

        var filesWithSize = MediaConversionHelper.BuildItemsWithSizes(acceptedFiles, static path => path, out var totalBytes)
            .Select(entry => (Path: entry.Item, entry.Size))
            .ToList();

        var copiedFiles = new List<string>();
        var sortedEpisodes = request.Episodes.OrderBy(episode => episode.EpisodeNumber).ToList();
        Directory.CreateDirectory(request.Destination);

        var copyStats = new List<FileCopyStats>();
        long completedBytes = 0;

        for (var fileIndex = 0; fileIndex < filesWithSize.Count; fileIndex++)
        {
            var (inputFile, fileSize) = filesWithSize[fileIndex];
            var episodeIndex = (request.EpisodeStart - 1) + fileIndex;
            if (episodeIndex >= sortedEpisodes.Count)
            {
                cmdlet.WriteWarning(
                    $"No TVDb episode metadata for file '{inputFile}'. " +
                    $"The season scan returned {sortedEpisodes.Count} episode(s), but -EpisodeStart {request.EpisodeStart} maps input files to scan positions " +
                    $"{request.EpisodeStart} onward (file {fileIndex + 1} needs position {episodeIndex + 1}). " +
                    "DVD-order TVDb season pages often have fewer rows than aired order; use .../seasons/official/<season> or lower -EpisodeStart.");
                break;
            }

            var episode = sortedEpisodes[episodeIndex];
            var destinationName = buildEpisodeFileName(request.Title, request.Season, episode, Path.GetExtension(inputFile));
            var destinationPath = Path.Combine(request.Destination, destinationName);

            var currentFileIndex = fileIndex + 1;
            var eta = CalculateCopyRemainingTime(completedBytes, totalBytes, copyStats);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                currentFileIndex, filesWithSize.Count, Path.GetFileName(inputFile), completedBytes, totalBytes);
            MediaConversionHelper.WriteMainProgress(cmdlet, "Video copy", status, percent, eta, ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(cmdlet, "Current file", "Copying...", destinationName, recordType: ProgressRecordType.Processing);

            var stopwatch = Stopwatch.StartNew();
            File.Copy(inputFile, destinationPath, true);
            stopwatch.Stop();

            copiedFiles.Add(destinationPath);
            completedBytes += fileSize;
            copyStats.Add(new FileCopyStats { FileSizeBytes = fileSize, ProcessingTime = stopwatch.Elapsed });

            (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                currentFileIndex, filesWithSize.Count, Path.GetFileName(inputFile), completedBytes, totalBytes);
            MediaConversionHelper.WriteMainProgress(cmdlet, "Video copy", status, percent, null, ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(cmdlet, "Current file", "Completed", destinationName, recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(cmdlet, "Video copy", "Current file");

        return copiedFiles;
    }

    private static TimeSpan? CalculateCopyRemainingTime(long completedBytes, long totalBytes, List<FileCopyStats> stats)
    {
        var remainingBytes = totalBytes - completedBytes;
        return MediaConversionHelper.CalculateRemainingTime(
            remainingBytes,
            stats.Select(s => (s.FileSizeBytes, s.ProcessingTime)));
    }

    private IReadOnlyList<string> ResolveDirectories(PSCmdlet cmdlet, IReadOnlyList<string> paths)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            try
            {
                var resolved = cmdlet.GetResolvedProviderPathFromPSPath(path, out _);
                foreach (var item in resolved)
                {
                    if (Directory.Exists(item))
                        directories.Add(item);
                }
            }
            catch
            {
                if (Directory.Exists(path))
                    directories.Add(Path.GetFullPath(path));
            }
        }

        return directories.ToList();
    }

    private sealed class FileCopyStats
    {
        public long FileSizeBytes { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public double BytesPerSecond => FileSizeBytes > 0 && ProcessingTime.TotalSeconds > 0
            ? FileSizeBytes / ProcessingTime.TotalSeconds
            : 0;
    }
}
