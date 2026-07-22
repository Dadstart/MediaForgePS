using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helper class for creating progress records for media conversion operations.
/// </summary>
public static class MediaConversionHelper
{
    private static readonly TimeSpan _defaultProgressPollInterval = TimeSpan.FromSeconds(0.05);
    private static readonly TimeSpan _defaultBatchProgressInterval = TimeSpan.FromSeconds(1.0);
    private static readonly string[] _encodeProgressSpinner = ["|", "/", "-", "\\"];

    /// <summary>
    /// Result of selecting preferred audio streams for automatic mapping.
    /// </summary>
    public readonly record struct AudioStreamSelection(
        IReadOnlyList<MediaStream> SelectedStreams,
        int TotalAudioStreamCount,
        int EnglishAudioStreamCount);

    /// <summary>
    /// Item-level encode progress update produced while <see cref="RunConversionWithProgress"/> polls Ffmpeg.
    /// </summary>
    public readonly record struct EncodeProgressUpdate(
        string Status,
        string CurrentOperation,
        int PercentComplete,
        TimeSpan? Eta);

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
    /// Converts a byte count to megabytes (MiB).
    /// </summary>
    public static double BytesToMegabytes(long bytes) =>
        bytes / (double)(1 << 20);

    /// <summary>
    /// Formats a byte count as megabytes with one decimal place (e.g. <c>1.5 MB</c>).
    /// </summary>
    public static string FormatMegabyteCount(long bytes) =>
        FormatMegabytes(BytesToMegabytes(bytes));

    /// <summary>
    /// Formats a megabyte value with one decimal place (e.g. <c>1.5 MB</c>).
    /// </summary>
    public static string FormatMegabytes(double megabytes) =>
        $"{megabytes:F1} MB";

    /// <summary>
    /// Formats a timespan as <c>mm:ss</c>, or <c>h:mm:ss</c> when one hour or longer.
    /// </summary>
    public static string FormatTimespan(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";

        return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
    }

    /// <summary>
    /// Returns the size of a file in bytes, or 0 when the path is missing or unreadable.
    /// </summary>
    public static long TryGetFileSizeBytes(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return 0;

        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Calculates percent of input size saved by conversion (positive means a smaller output).
    /// </summary>
    public static double? CalculateSizeReductionPercent(long inputBytes, long outputBytes)
    {
        if (inputBytes <= 0)
            return null;

        return Math.Round((1.0 - (double)outputBytes / inputBytes) * 100.0, 1);
    }

    /// <summary>
    /// Builds a <see cref="MediaConversionResult"/> from paths, status, and elapsed processing time.
    /// </summary>
    public static MediaConversionResult CreateConversionResult(
        string inputPath,
        string outputPath,
        bool success,
        string status,
        TimeSpan processingTime)
    {
        var inputSizeBytes = TryGetFileSizeBytes(inputPath);
        var outputSizeBytes = success ? TryGetFileSizeBytes(outputPath) : 0L;
        var reduction = success ? CalculateSizeReductionPercent(inputSizeBytes, outputSizeBytes) : null;

        return new MediaConversionResult(
            inputPath,
            outputPath,
            status,
            BytesToMegabytes(inputSizeBytes),
            BytesToMegabytes(outputSizeBytes),
            reduction,
            processingTime);
    }

    /// <summary>
    /// Whether the result status indicates a completed conversion.
    /// </summary>
    public static bool IsCompletedConversion(MediaConversionResult result) =>
        string.Equals(result.Status, MediaConversionResult.CompletedStatus, StringComparison.Ordinal);

    /// <summary>
    /// Whether the result status indicates a WhatIf / ShouldProcess skip.
    /// </summary>
    public static bool IsWhatIfConversion(MediaConversionResult result) =>
        string.Equals(result.Status, MediaConversionResult.WhatIfStatus, StringComparison.Ordinal);

    /// <summary>
    /// Formats a size-reduction percent as a short human-readable phrase.
    /// </summary>
    public static string FormatSizeReduction(double? sizeReductionPercent)
    {
        if (!sizeReductionPercent.HasValue)
            return "n/a";

        var percent = sizeReductionPercent.Value;
        if (percent > 0)
            return $"{percent:0.#}% smaller";
        if (percent < 0)
            return $"{Math.Abs(percent):0.#}% larger";

        return "same size";
    }

    /// <summary>
    /// Formats a conversion result for host summaries (file name, size change, and duration).
    /// </summary>
    public static string FormatConversionResultLine(MediaConversionResult result)
    {
        if (!IsCompletedConversion(result))
            return $"{PathHelper.GetFileName(result.InputPath)} — {result.Status}";

        var sizeChange = FormatSizeReduction(result.SizeReductionPercent);
        var sizes = $"{FormatMegabytes(result.InputSizeMegabytes)} → {FormatMegabytes(result.OutputSizeMegabytes)}";
        return $"{PathHelper.GetFileName(result.OutputPath)} — {sizeChange} ({sizes}) in {FormatTimespan(result.ProcessingTime)}";
    }

    /// <summary>
    /// Formats batch conversion averages for host summaries.
    /// </summary>
    public static string FormatConversionStatisticsLine(MediaConversionStatistics statistics)
    {
        if (statistics.FileCount <= 0)
            return "Averages — n/a (0 files)";

        var sizeChange = FormatSizeReduction(statistics.AverageSizeReductionPercent);
        var sizes = $"{FormatMegabytes(statistics.AverageInputSizeMegabytes)} → {FormatMegabytes(statistics.AverageOutputSizeMegabytes)}";
        var fileLabel = statistics.FileCount == 1 ? "1 file" : $"{statistics.FileCount} files";
        return $"Averages — {sizeChange} ({sizes}) in {FormatTimespan(statistics.AverageProcessingTime)} ({fileLabel})";
    }

    /// <summary>
    /// Builds aggregate averages from completed conversion results.
    /// </summary>
    public static MediaConversionStatistics CreateConversionStatistics(
        IEnumerable<MediaConversionResult>? results) =>
        MediaConversionStatistics.Create(results);

    /// <summary>
    /// Builds encode status text including media position as <c>mm:ss / mm:ss</c>.
    /// </summary>
    public static string BuildEncodeProgressStatus(string baseStatus, FfmpegProgress progress)
    {
        var outTime = FormatTimespan(progress.OutTime);
        if (progress.TotalDuration > TimeSpan.Zero)
            return $"{baseStatus} — {outTime} / {FormatTimespan(progress.TotalDuration)}";

        return $"{baseStatus} — {outTime}";
    }

    /// <summary>
    /// Whether encode ETA is one second or less and the finishing spinner should be shown.
    /// </summary>
    public static bool IsEncodeFinishing(FfmpegProgress progress) =>
        progress.EstimatedTimeRemaining is { TotalSeconds: <= 1 };

    /// <summary>
    /// Builds the next <c>finishing</c> status frame and advances the spinner index.
    /// </summary>
    public static string BuildEncodeFinishingStatus(string[] spinner, ref int spinnerIndex)
    {
        var frame = spinner[spinnerIndex];
        spinnerIndex = (spinnerIndex + 1) % spinner.Length;
        return $"finishing {frame}";
    }

    /// <summary>
    /// Builds encode progress display text and ETA, switching to a finishing spinner near completion.
    /// </summary>
    public static (string Status, TimeSpan? Eta) BuildEncodeProgressDisplay(
        string baseStatus,
        FfmpegProgress progress,
        string[] spinner,
        ref int spinnerIndex)
    {
        if (IsEncodeFinishing(progress))
            return (BuildEncodeFinishingStatus(spinner, ref spinnerIndex), null);

        return (BuildEncodeProgressStatus(baseStatus, progress), progress.EstimatedTimeRemaining);
    }

    /// <summary>
    /// Runs conversion work on a background thread while polling Ffmpeg progress on the calling thread.
    /// Keeps PowerShell progress writes on the cmdlet thread and avoids SynchronizationContext deadlocks.
    /// </summary>
    /// <param name="convert">Conversion work that reports Ffmpeg progress and honors cancellation.</param>
    /// <param name="encodeStatus">Base status text shown while encoding (e.g. codec and preset).</param>
    /// <param name="currentOperation">Current-item progress operation (typically the output file name).</param>
    /// <param name="reportItemProgress">Callback that writes nested item progress on the calling thread.</param>
    /// <param name="cancellationToken">Token used to stop polling and cancel the conversion.</param>
    /// <param name="reportBatchProgress">Optional callback invoked on a timer for top-level batch progress.</param>
    /// <param name="pollInterval">How often to poll for encode progress. Defaults to 50ms.</param>
    /// <param name="batchUpdateInterval">How often to invoke <paramref name="reportBatchProgress"/>. Defaults to 1s.</param>
    public static void RunConversionWithProgress(
        Action<IProgress<FfmpegProgress>, CancellationToken> convert,
        string encodeStatus,
        string currentOperation,
        Action<EncodeProgressUpdate> reportItemProgress,
        CancellationToken cancellationToken,
        Action? reportBatchProgress = null,
        TimeSpan? pollInterval = null,
        TimeSpan? batchUpdateInterval = null)
    {
        ArgumentNullException.ThrowIfNull(convert);
        ArgumentNullException.ThrowIfNull(reportItemProgress);
        ArgumentException.ThrowIfNullOrWhiteSpace(encodeStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentOperation);

        var poll = pollInterval ?? _defaultProgressPollInterval;
        var batchInterval = batchUpdateInterval ?? _defaultBatchProgressInterval;

        reportItemProgress(new EncodeProgressUpdate(encodeStatus, currentOperation, 0, null));

        var encodeProgress = new LatestFfmpegProgress();
        var spinnerIndex = 0;
        var lastBatchUpdateTime = DateTime.UtcNow;
        var conversionTask = Task.Run(() => convert(encodeProgress, cancellationToken));

        while (Task.WaitAny([conversionTask], poll) < 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var latest = encodeProgress.Latest;
            if (latest is not null)
            {
                var (status, eta) = BuildEncodeProgressDisplay(
                    encodeStatus,
                    latest,
                    _encodeProgressSpinner,
                    ref spinnerIndex);
                reportItemProgress(new EncodeProgressUpdate(
                    status,
                    currentOperation,
                    latest.PercentComplete,
                    eta));
            }
            else
            {
                var indicator = _encodeProgressSpinner[spinnerIndex];
                spinnerIndex = (spinnerIndex + 1) % _encodeProgressSpinner.Length;
                reportItemProgress(new EncodeProgressUpdate(
                    $"{encodeStatus} {indicator}",
                    currentOperation,
                    0,
                    null));
            }

            if (reportBatchProgress is null)
                continue;

            var now = DateTime.UtcNow;
            if (now - lastBatchUpdateTime < batchInterval)
                continue;

            reportBatchProgress();
            lastBatchUpdateTime = now;
        }

        conversionTask.GetAwaiter().GetResult();
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
    /// Sums byte sizes for the current file and all files after it in an ordered batch list.
    /// </summary>
    /// <param name="orderedSizes">File sizes in processing order.</param>
    /// <param name="currentOneBasedIndex">1-based index of the file currently being processed.</param>
    /// <returns>Remaining bytes including the current file, or 0 when the index is out of range.</returns>
    public static long SumRemainingBytesFromOrderedSizes(IReadOnlyList<long> orderedSizes, int currentOneBasedIndex)
    {
        ArgumentNullException.ThrowIfNull(orderedSizes);

        if (currentOneBasedIndex < 1 || orderedSizes.Count == 0)
            return 0;

        var startIndex = currentOneBasedIndex - 1;
        if (startIndex >= orderedSizes.Count)
            return 0;

        long remainingBytes = 0;
        for (var i = startIndex; i < orderedSizes.Count; i++)
            remainingBytes += orderedSizes[i];

        return remainingBytes;
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
    /// Creates default video encoding settings for a named encoder preset.
    /// </summary>
    /// <param name="defaultVideoEncoder">Encoder name: x264, x265, or nvenc. When null or unrecognized, libx265 is used.</param>
    /// <returns>Video encoding settings for the resolved codec.</returns>
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
    /// <param name="selectedStreams">Selected audio streams to map.</param>
    /// <param name="allStreams">
    /// All streams from the media file; used for FFmpeg <c>-map 0:a:N</c> ordinals.
    /// When omitted, <paramref name="selectedStreams"/> is treated as the full stream set.
    /// </param>
    /// <returns>Array of conversion mappings.</returns>
    public static AudioTrackMapping[] CreateAutomaticAudioTrackMappings(
        IEnumerable<MediaStream> selectedStreams,
        IEnumerable<MediaStream>? allStreams = null)
    {
        return AudioTrackMappingService.CreateAutomaticMappingsFromStreams(
            selectedStreams,
            allStreams ?? selectedStreams);
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
        ICmdletProgress progress,
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
        ApplyEta(progressRecord, eta);
        progress.WriteProgress(progressRecord);
    }

    /// <summary>
    /// Writes the current-item (nested) progress record.
    /// </summary>
    public static void WriteCurrentItemProgress(
        ICmdletProgress progress,
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
        ApplyEta(progressRecord, eta);
        progress.WriteProgress(progressRecord);
    }

    private static void ApplyEta(ProgressRecord progressRecord, TimeSpan? eta)
    {
        if (!eta.HasValue)
            return;

        var clampedSeconds = (int)Math.Clamp(Math.Ceiling(eta.Value.TotalSeconds), 0, int.MaxValue);
        progressRecord.SecondsRemaining = clampedSeconds;
    }

    /// <summary>
    /// Writes both main and current-item progress records as completed.
    /// Call once when the batch or phase is finished.
    /// </summary>
    public static void WriteProgressCompleted(ICmdletProgress progress, string mainActivity, string currentActivity)
    {
        progress.WriteProgress(CreateSimpleProgressRecord(
            ProgressActivityIds.Main,
            mainActivity,
            "Completed",
            recordType: ProgressRecordType.Completed));
        progress.WriteProgress(CreateSimpleProgressRecord(
            ProgressActivityIds.CurrentItem,
            currentActivity,
            "Completed",
            recordType: ProgressRecordType.Completed));
    }
}
