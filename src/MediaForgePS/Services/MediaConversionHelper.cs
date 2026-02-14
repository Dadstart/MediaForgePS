using System;
using System.Management.Automation;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helper class for creating progress records for media conversion operations.
/// </summary>
public static class MediaConversionHelper
{
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
