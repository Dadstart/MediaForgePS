using System;
using System.Management.Automation;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helper class for creating progress records for media conversion operations.
/// </summary>
public static class MediaConversionHelper
{
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
