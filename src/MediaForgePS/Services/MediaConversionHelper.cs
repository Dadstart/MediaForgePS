using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helper class for creating progress records for media conversion operations.
/// </summary>
public static class MediaConversionHelper
{
    /// <summary>
    /// Result of selecting preferred audio streams for automatic mapping.
    /// </summary>
    public readonly record struct AudioStreamSelection(
        IReadOnlyList<MediaStream> SelectedStreams,
        int TotalAudioStreamCount,
        int EnglishAudioStreamCount);

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
    /// Builds a list of items paired with file size and computes total bytes.
    /// </summary>
    public static IReadOnlyList<(T Item, long Size)> BuildItemsWithSizes<T>(
        IEnumerable<T> items,
        Func<T, string> pathSelector,
        out long totalBytes)
    {
        totalBytes = 0;
        var sizedItems = new List<(T Item, long Size)>();
        foreach (var item in items)
        {
            long size = 0;
            try
            {
                var fileInfo = new FileInfo(pathSelector(item));
                if (fileInfo.Exists)
                {
                    size = fileInfo.Length;
                    totalBytes += size;
                }
            }
            catch
            {
                // Use 0 for this item.
            }

            sizedItems.Add((item, size));
        }

        return sizedItems;
    }

    /// <summary>
    /// Selects preferred audio streams: English audio streams when present, otherwise all audio streams.
    /// </summary>
    public static AudioStreamSelection SelectPreferredAudioStreams(IEnumerable<MediaStream> streams)
    {
        var audioStreams = streams
            .Where(s => string.Equals(s.Type, "audio", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (audioStreams.Count == 0)
            return new AudioStreamSelection(Array.Empty<MediaStream>(), 0, 0);

        var englishAudioStreams = audioStreams
            .Where(s => string.Equals(s.Language, "eng", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return englishAudioStreams.Count > 0
            ? new AudioStreamSelection(englishAudioStreams, audioStreams.Count, englishAudioStreams.Count)
            : new AudioStreamSelection(audioStreams, audioStreams.Count, 0);
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
    /// Creates default video encoding settings for a default encoder value.
    /// </summary>
    /// <param name="defaultVideoEncoder">Default encoder name (x264, x265, nvenc).</param>
    /// <returns>Video encoding settings instance.</returns>
    public static VideoEncodingSettings CreateDefaultVideoEncodingSettings(string? defaultVideoEncoder)
    {
        var encoder = defaultVideoEncoder?.Trim();
        var codec = encoder?.ToLowerInvariant() switch
        {
            "nvenc" => "nvenc",
            "x264" => "libx264",
            _ => "libx265"
        };

        if (codec == "nvenc")
        {
            return new NvencVideoEncodingSettings(
                "p5",
                18);
        }

        return new ConstantRateVideoEncodingSettings(
            codec,
            "medium",
            "high",
            "film",
            18,
            VideoEncodingSettings.GetDefaultPixelFormat(codec));
    }

    /// <summary>
    /// Creates automatic audio track mappings for conversion from selected streams.
    /// </summary>
    /// <param name="streams">Selected audio streams to map.</param>
    /// <returns>Array of conversion mappings.</returns>
    public static AudioTrackMapping[] CreateAutomaticAudioTrackMappings(IEnumerable<MediaStream> streams)
    {
        var mappings = new List<AudioTrackMapping>();
        var destinationIndex = 0;

        foreach (var stream in streams)
        {
            var channels = AudioTrackMappingService.ParseChannelCount(stream.Raw);
            stream.Tags.TryGetValue("title", out var title);

            AudioTrackMapping mapping;
            var codecLower = stream.Codec.ToLowerInvariant();
            if ((codecLower == "dts" || codecLower == "truehd") && channels >= 6 && !string.Equals(stream.Profile, "dts", StringComparison.OrdinalIgnoreCase))
            {
                mapping = new CopyAudioTrackMapping(
                    title,
                    0,
                    stream.Index - 1,
                    destinationIndex);
            }
            else
            {
                mapping = new EncodeAudioTrackMapping(
                    title,
                    0,
                    stream.Index - 1,
                    destinationIndex,
                    "aac",
                    0,
                    channels);
            }

            mappings.Add(mapping);
            destinationIndex++;
        }

        if (mappings.Count >= 2 &&
            mappings[0] is CopyAudioTrackMapping copyMapping &&
            mappings[1] is EncodeAudioTrackMapping encodeMapping &&
            string.Equals(encodeMapping.DestinationCodec, "aac", StringComparison.OrdinalIgnoreCase) &&
            encodeMapping.DestinationChannels >= 6 &&
            copyMapping.SourceIndex < encodeMapping.SourceIndex)
        {
            mappings[0] = new EncodeAudioTrackMapping(
                encodeMapping.Title,
                encodeMapping.SourceStream,
                encodeMapping.SourceIndex,
                copyMapping.DestinationIndex,
                encodeMapping.DestinationCodec,
                encodeMapping.DestinationBitrate,
                encodeMapping.DestinationChannels);

            mappings[1] = new CopyAudioTrackMapping(
                copyMapping.Title,
                copyMapping.SourceStream,
                copyMapping.SourceIndex,
                encodeMapping.DestinationIndex);
        }

        return mappings.ToArray();
    }

    /// <summary>
    /// Builds a user-facing status message from an FFmpeg conversion exception.
    /// </summary>
    /// <param name="exception">Conversion exception instance.</param>
    /// <returns>Short status message with exit code and first error line when available.</returns>
    public static string BuildConversionFailureStatusMessage(FfmpegConversionException exception)
    {
        var message = "Conversion failed";
        if (exception.ExitCode.HasValue)
            message += $" (exit code: {exception.ExitCode.Value})";
        if (!string.IsNullOrWhiteSpace(exception.ErrorOutput))
        {
            var errorLines = exception.ErrorOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (errorLines.Length > 0)
            {
                var firstErrorLine = errorLines[0].Trim();
                if (firstErrorLine.Length > 0)
                    message += $": {firstErrorLine}";
            }
        }

        return message;
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

    /// <summary>
    /// Writes the main (batch) progress record and optionally the current-item record.
    /// Use with <see cref="BuildBatchProgressStatus"/> or <see cref="BuildCountBasedProgressStatus"/> for status and percent.
    /// </summary>
    public static void WriteMainProgress(
        PSCmdlet cmdlet,
        string mainActivity,
        string status,
        int? percentComplete = null,
        TimeSpan? eta = null,
        ProgressRecordType recordType = ProgressRecordType.Processing)
    {
        var progressRecord = CreateSimpleProgressRecord(
            ProgressActivityIds.Main,
            mainActivity,
            status,
            percentComplete,
            recordType: recordType);
        if (eta.HasValue)
            progressRecord.StatusDescription = $"ETA: {FormatTimespan(eta.Value)}";
        cmdlet.WriteProgress(progressRecord);
    }

    /// <summary>
    /// Writes the current-item (nested) progress record.
    /// </summary>
    public static void WriteCurrentItemProgress(
        PSCmdlet cmdlet,
        string currentActivity,
        string status,
        string? currentOperation = null,
        int? percentComplete = null,
        TimeSpan? eta = null,
        ProgressRecordType recordType = ProgressRecordType.Processing)
    {
        var progressRecord = CreateNestedProgressRecord(
            ProgressActivityIds.CurrentItem,
            currentActivity,
            status,
            ProgressActivityIds.Main,
            currentOperation,
            percentComplete,
            recordType);
        if (eta.HasValue)
            progressRecord.StatusDescription = $"File ETA: {FormatTimespan(eta.Value)}";
        cmdlet.WriteProgress(progressRecord);
    }

    /// <summary>
    /// Writes both main and current-item progress records as completed.
    /// Call once when the batch or phase is finished.
    /// </summary>
    public static void WriteProgressCompleted(PSCmdlet cmdlet, string mainActivity, string currentActivity)
    {
        cmdlet.WriteProgress(CreateSimpleProgressRecord(
            ProgressActivityIds.Main,
            mainActivity,
            "Completed",
            recordType: ProgressRecordType.Completed));
        cmdlet.WriteProgress(CreateSimpleProgressRecord(
            ProgressActivityIds.CurrentItem,
            currentActivity,
            "Completed",
            recordType: ProgressRecordType.Completed));
    }
}
