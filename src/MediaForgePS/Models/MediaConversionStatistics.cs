using System;
using System.Collections.Generic;
using System.Linq;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Aggregate averages across completed media conversions in a batch.
/// </summary>
/// <param name="FileCount">Number of completed conversions included in the averages.</param>
/// <param name="AverageSizeReductionPercent">
/// Mean of per-file size-reduction percents (positive means smaller output). Null when no completed conversions.
/// </param>
/// <param name="AverageInputSizeMegabytes">Mean input file size in megabytes (MiB) across completed conversions.</param>
/// <param name="AverageOutputSizeMegabytes">Mean output file size in megabytes (MiB) across completed conversions.</param>
/// <param name="AverageProcessingTime">Mean wall-clock processing time across completed conversions.</param>
public sealed record MediaConversionStatistics(
    int FileCount,
    double? AverageSizeReductionPercent,
    double AverageInputSizeMegabytes,
    double AverageOutputSizeMegabytes,
    TimeSpan AverageProcessingTime)
{
    /// <summary>
    /// Empty statistics with zero counts and null size reduction.
    /// </summary>
    public static MediaConversionStatistics Empty { get; } =
        new(0, null, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Builds averages from completed conversion results. Non-completed results are ignored.
    /// </summary>
    public static MediaConversionStatistics Create(IEnumerable<MediaConversionResult>? results)
    {
        if (results is null)
            return Empty;

        var completed = results
            .Where(r => string.Equals(r.Status, MediaConversionResult.CompletedStatus, StringComparison.Ordinal))
            .ToList();

        if (completed.Count == 0)
            return Empty;

        var totalInput = 0.0;
        var totalOutput = 0.0;
        var totalTicks = 0L;
        var reductionSum = 0.0;
        var reductionCount = 0;

        foreach (var result in completed)
        {
            totalInput += result.InputSizeMegabytes;
            totalOutput += result.OutputSizeMegabytes;
            totalTicks += result.ProcessingTime.Ticks;

            if (!result.SizeReductionPercent.HasValue)
                continue;

            reductionSum += result.SizeReductionPercent.Value;
            reductionCount++;
        }

        var count = completed.Count;
        double? averageReduction = reductionCount > 0
            ? Math.Round(reductionSum / reductionCount, 1)
            : null;

        return new MediaConversionStatistics(
            count,
            averageReduction,
            Math.Round(totalInput / count, 1),
            Math.Round(totalOutput / count, 1),
            TimeSpan.FromTicks(totalTicks / count));
    }
}
