using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helper class for creating progress records for media conversion operations.
/// </summary>
public static class MediaConversionHelper
{
    /// <summary>
    /// Creates a progress record for Ffmpeg conversion progress.
    /// </summary>
    /// <param name="progress">The Ffmpeg progress information.</param>
    /// <param name="totalDurationMs">Total duration of the input file in milliseconds, if available.</param>
    /// <param name="status">Status message to display.</param>
    /// <param name="activity">Activity name for the progress record.</param>
    /// <param name="activityId">Activity ID for the progress record.</param>
    /// <param name="parentActivityId">Parent activity ID for nested progress records.</param>
    /// <returns>A ProgressRecord with the conversion progress details.</returns>
    public static ProgressRecord CreateProgressRecord(
        FfmpegProgress progress,
        long? totalDurationMs,
        string status,
        string activity = "Converting Media File",
        int activityId = 0,
        int? parentActivityId = null)
    {
        var progressRecord = new ProgressRecord(activityId, activity, status);

        if (parentActivityId.HasValue)
        {
            progressRecord.ParentActivityId = parentActivityId.Value;
        }

        // Calculate percentage if we have both current time and total duration
        if (progress.OutTimeMs.HasValue && totalDurationMs.HasValue && totalDurationMs.Value > 0)
        {
            var percentComplete = (int)global::System.Math.Min(100, global::System.Math.Max(0, (progress.OutTimeMs.Value * 100) / totalDurationMs.Value));
            progressRecord.PercentComplete = percentComplete;
        }

        // Build status details
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(progress.OutTime))
            details.Add($"Time: {progress.OutTime}");

        if (progress.Fps.HasValue)
            details.Add($"FPS: {progress.Fps:F2}");

        if (progress.Speed.HasValue)
            details.Add($"Speed: {progress.Speed:F2}x");

        if (progress.Bitrate.HasValue)
            details.Add($"Bitrate: {progress.Bitrate:F0} kbits/s");

        if (progress.Frame.HasValue)
            details.Add($"Frame: {progress.Frame:N0}");

        if (details.Count > 0)
            progressRecord.CurrentOperation = string.Join(" | ", details);

        // Mark as completed if progress indicates end
        if (progress.Progress == "end")
            progressRecord.RecordType = ProgressRecordType.Completed;

        return progressRecord;
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
}
