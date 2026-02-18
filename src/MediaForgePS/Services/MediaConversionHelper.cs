using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helper class for creating progress records for media conversion operations.
/// </summary>
public static class MediaConversionHelper
{
    /// <summary>
    /// Formats a byte count as a human-readable string (B, KB, MB, GB).
    /// </summary>
    public static string FormatByteCount(long bytes)
    {
        if (bytes >= 1 << 30)
            return $"{bytes / (double)(1 << 30):F1} GB";
        if (bytes >= 1 << 20)
            return $"{bytes / (double)(1 << 20):F1} MB";
        if (bytes >= 1 << 10)
            return $"{bytes / (double)(1 << 10):F1} KB";
        return $"{bytes} B";
    }

    /// <summary>
    /// Formats a timespan as a human-readable string (e.g. "2m 30s", "1h 5m 0s").
    /// </summary>
    public static string FormatTimespan(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{time.Hours}h {time.Minutes}m {time.Seconds}s";
        if (time.TotalMinutes >= 1)
            return $"{time.Minutes}m {time.Seconds}s";
        return $"{time.Seconds}s";
    }

    /// <summary>
    /// Formats seconds as an Ffmpeg-compatible timecode string (hh:mm:ss.fff).
    /// </summary>
    /// <param name="seconds">Time in seconds.</param>
    /// <returns>Formatted timecode string.</returns>
    public static string FormatTimeCode(double seconds)
    {
        var hours = (int)Math.Floor(seconds / 3600);
        var minutes = (int)Math.Floor((seconds % 3600) / 60);
        var secs = seconds % 60;
        return $"{hours:D2}:{minutes:D2}:{secs:00.000}";
    }

    /// <summary>
    /// Builds status text and percent for size-based batch progress (e.g. "File 2 of 5 (35%) — 120.5 MB / 350.2 MB — filename").
    /// </summary>
    /// <param name="currentFileIndex">Current file number (1-based).</param>
    /// <param name="totalFiles">Total number of files.</param>
    /// <param name="currentFileName">Display name of the current file.</param>
    /// <param name="completedBytes">Bytes processed so far.</param>
    /// <param name="totalBytes">Total bytes to process (0 to use count-based percent).</param>
    /// <returns>Status string and percent complete (0-100).</returns>
    public static (string Status, int Percent) BuildBatchProgressStatus(
        int currentFileIndex,
        int totalFiles,
        string currentFileName,
        long completedBytes,
        long totalBytes)
    {
        var percent = totalBytes > 0
            ? (int)((completedBytes * 100.0) / totalBytes)
            : (int)((currentFileIndex * 100.0) / totalFiles);
        var status = $"File {currentFileIndex} of {totalFiles} ({percent}%)";
        if (totalBytes > 0)
            status += $" — {FormatByteCount(completedBytes)} / {FormatByteCount(totalBytes)}";
        status += $" — {currentFileName}";
        return (status, percent);
    }

    /// <summary>
    /// Builds status text and percent for count-based progress (e.g. "File 2 of 5 (40%) — filename").
    /// </summary>
    public static (string Status, int Percent) BuildCountBasedProgressStatus(
        int currentFileIndex,
        int totalFiles,
        string currentFileName)
    {
        var percent = totalFiles > 0 ? (int)((currentFileIndex * 100.0) / totalFiles) : 0;
        var status = $"File {currentFileIndex} of {totalFiles} ({percent}%) — {currentFileName}";
        return (status, percent);
    }

    /// <summary>
    /// Calculates estimated remaining time from remaining bytes and completed processing stats.
    /// </summary>
    /// <param name="remainingBytes">Bytes not yet processed.</param>
    /// <param name="completedStats">Completed items as (FileSizeBytes, ProcessingTime) for average throughput.</param>
    /// <returns>Estimated time remaining, or null if not enough data.</returns>
    public static TimeSpan? CalculateRemainingTime(
        long remainingBytes,
        IEnumerable<(long FileSizeBytes, TimeSpan ProcessingTime)> completedStats)
    {
        var stats = completedStats as (long FileSizeBytes, TimeSpan ProcessingTime)[] ?? completedStats.ToArray();
        if (stats.Length == 0)
            return null;

        var totalBytes = 0L;
        var totalSeconds = 0.0;
        foreach (var (fileSizeBytes, processingTime) in stats)
        {
            totalBytes += fileSizeBytes;
            totalSeconds += processingTime.TotalSeconds;
        }

        if (totalBytes <= 0 || totalSeconds <= 0)
            return null;
        if (remainingBytes <= 0)
            return null;

        var averageBytesPerSecond = totalBytes / totalSeconds;
        return TimeSpan.FromSeconds(remainingBytes / averageBytesPerSecond);
    }

    /// <summary>
    /// Builds x265 parameters for Ffmpeg when applicable.
    /// </summary>
    /// <param name="x265Params">Raw x265 params string (passed via -x265-params).</param>
    /// <param name="codec">Video codec name to determine x265 compatibility.</param>
    /// <returns>x265 arguments or null when not applicable.</returns>
    public static string[]? BuildX265Arguments(string? x265Params, string codec)
    {
        if (!string.IsNullOrWhiteSpace(x265Params) && IsX265Codec(codec))
            return ["-x265-params", x265Params];

        return null;
    }

    /// <summary>
    /// Determines whether the provided codec name targets x265 encoding.
    /// </summary>
    /// <param name="codec">Codec name to evaluate.</param>
    /// <returns>True when the codec name indicates x265 encoding.</returns>
    public static bool IsX265Codec(string codec)
    {
        return !string.IsNullOrWhiteSpace(codec) &&
               codec.Contains("265", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a simple progress record without Ffmpeg progress data.
    /// </summary>
    /// <param name="activityId">Activity ID for the progress record.</param>
    /// <param name="activity">Activity name for the progress record.</param>
    /// <param name="status">Status message to display.</param>
    /// <param name="percentComplete">Percentage complete (0-100).</param>
    /// <param name="parentActivityId">Parent activity ID for nested progress records.</param>
    /// <param name="recordType">Record type (defaults to Processing).</param>
    /// <returns>A ProgressRecord with the specified details.</returns>
    public static ProgressRecord CreateSimpleProgressRecord(
        int activityId,
        string activity,
        string status,
        int? percentComplete = null,
        int? parentActivityId = null,
        ProgressRecordType recordType = ProgressRecordType.Processing)
    {
        var progressRecord = new ProgressRecord(activityId, activity, status)
        {
            RecordType = recordType
        };

        if (parentActivityId.HasValue)
        {
            progressRecord.ParentActivityId = parentActivityId.Value;
        }

        if (percentComplete.HasValue)
        {
            progressRecord.PercentComplete = percentComplete.Value;
        }

        return progressRecord;
    }

    /// <summary>
    /// Creates a nested progress record with optional current operation text.
    /// </summary>
    /// <param name="activityId">Activity ID for the progress record.</param>
    /// <param name="activity">Activity name for the progress record.</param>
    /// <param name="status">Status message to display.</param>
    /// <param name="parentActivityId">Parent activity ID for nested progress records.</param>
    /// <param name="currentOperation">Current operation text to display.</param>
    /// <param name="percentComplete">Percentage complete (0-100 or -1 for indeterminate).</param>
    /// <param name="recordType">Record type (defaults to Processing).</param>
    /// <returns>A ProgressRecord with the specified details.</returns>
    public static ProgressRecord CreateNestedProgressRecord(
        int activityId,
        string activity,
        string status,
        int parentActivityId,
        string? currentOperation = null,
        int? percentComplete = null,
        ProgressRecordType recordType = ProgressRecordType.Processing)
    {
        var progressRecord = CreateSimpleProgressRecord(
            activityId,
            activity,
            status,
            percentComplete,
            parentActivityId,
            recordType);

        if (!string.IsNullOrWhiteSpace(currentOperation))
            progressRecord.CurrentOperation = currentOperation;

        return progressRecord;
    }
}
