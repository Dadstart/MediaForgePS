using System;
using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class BatchProgressEstimatorTests
{
    [Fact]
    public void SumRemainingBytesFromOrderedSizes_UsesOneBasedIndexAgainstOrderedList()
    {
        var sizes = new long[] { 100, 200, 300, 400 };

        Assert.Equal(1000, MediaConversionHelper.SumRemainingBytesFromOrderedSizes(sizes, currentOneBasedIndex: 1));
        Assert.Equal(900, MediaConversionHelper.SumRemainingBytesFromOrderedSizes(sizes, currentOneBasedIndex: 2));
        Assert.Equal(700, MediaConversionHelper.SumRemainingBytesFromOrderedSizes(sizes, currentOneBasedIndex: 3));
        Assert.Equal(400, MediaConversionHelper.SumRemainingBytesFromOrderedSizes(sizes, currentOneBasedIndex: 4));
        Assert.Equal(0, MediaConversionHelper.SumRemainingBytesFromOrderedSizes(sizes, currentOneBasedIndex: 5));
        Assert.Equal(0, MediaConversionHelper.SumRemainingBytesFromOrderedSizes(sizes, currentOneBasedIndex: 0));
    }

    [Fact]
    public void EstimateRemaining_UsesOrderedSizedPathsNotHashSetOrder()
    {
        var estimator = new BatchProgressEstimator();
        estimator.RecordCompleted(100, TimeSpan.FromSeconds(1));

        IReadOnlyList<(string Path, long Size)> ordered =
        [
            ("a.mkv", 100),
            ("b.mkv", 200),
            ("c.mkv", 300)
        ];

        // Current file is b (index 2): remaining = 200 + 300 = 500 at 100 bytes/sec => 5s
        var eta = estimator.EstimateRemaining(ordered, currentOneBasedIndex: 2);

        Assert.NotNull(eta);
        Assert.Equal(TimeSpan.FromSeconds(5), eta.Value);
    }

    [Fact]
    public void EstimateFile_UsesAverageThroughputFromCompletedStats()
    {
        var estimator = new BatchProgressEstimator();
        estimator.RecordCompleted(1000, TimeSpan.FromSeconds(10));

        var eta = estimator.EstimateFile(500);

        Assert.NotNull(eta);
        Assert.Equal(TimeSpan.FromSeconds(5), eta.Value);
    }

    [Fact]
    public void Reset_ClearsCompletedStats()
    {
        var estimator = new BatchProgressEstimator();
        estimator.RecordCompleted(100, TimeSpan.FromSeconds(1));
        Assert.Equal(1, estimator.CompletedCount);

        estimator.Reset();

        Assert.Equal(0, estimator.CompletedCount);
        Assert.Null(estimator.EstimateFile(100));
    }
}
