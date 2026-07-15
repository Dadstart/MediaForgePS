using System;
using System.Collections.Generic;
using System.Linq;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Tracks completed file throughput and estimates remaining batch work from ordered sized paths.
/// </summary>
public sealed class BatchProgressEstimator
{
    private readonly List<(long FileSizeBytes, TimeSpan ProcessingTime)> _completedStats = new();

    /// <summary>
    /// Number of completed files recorded for throughput calculations.
    /// </summary>
    public int CompletedCount => _completedStats.Count;

    /// <summary>
    /// Clears recorded throughput stats for a new batch.
    /// </summary>
    public void Reset() => _completedStats.Clear();

    /// <summary>
    /// Records throughput stats for a completed file.
    /// </summary>
    public void RecordCompleted(long fileSizeBytes, TimeSpan processingTime)
    {
        if (fileSizeBytes <= 0 || processingTime <= TimeSpan.Zero)
            return;

        _completedStats.Add((fileSizeBytes, processingTime));
    }

    /// <summary>
    /// Estimates remaining batch time using ordered file sizes and recorded throughput.
    /// </summary>
    /// <param name="orderedSizedPaths">Input paths with sizes in processing order.</param>
    /// <param name="currentOneBasedIndex">1-based index of the file currently being processed.</param>
    public TimeSpan? EstimateRemaining(
        IReadOnlyList<(string Path, long Size)> orderedSizedPaths,
        int currentOneBasedIndex)
    {
        ArgumentNullException.ThrowIfNull(orderedSizedPaths);

        var remainingBytes = MediaConversionHelper.SumRemainingBytesFromOrderedSizes(
            orderedSizedPaths.Select(entry => entry.Size).ToArray(),
            currentOneBasedIndex);

        return MediaConversionHelper.CalculateRemainingTime(remainingBytes, _completedStats);
    }

    /// <summary>
    /// Estimates time to process a single file from its size and average recorded throughput.
    /// </summary>
    public TimeSpan? EstimateFile(long fileSizeBytes)
    {
        if (fileSizeBytes <= 0 || _completedStats.Count == 0)
            return null;

        var totalBytes = _completedStats.Sum(stat => stat.FileSizeBytes);
        var totalSeconds = _completedStats.Sum(stat => stat.ProcessingTime.TotalSeconds);
        if (totalBytes <= 0 || totalSeconds <= 0)
            return null;

        var averageBytesPerSecond = totalBytes / totalSeconds;
        if (averageBytesPerSecond <= 0)
            return null;

        return TimeSpan.FromSeconds(fileSizeBytes / averageBytesPerSecond);
    }
}
