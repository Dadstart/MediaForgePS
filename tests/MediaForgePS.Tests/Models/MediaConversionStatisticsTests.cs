using System;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class MediaConversionStatisticsTests
{
    [Fact]
    public void Empty_HasZeroCountsAndNullReduction()
    {
        var statistics = MediaConversionStatistics.Empty;

        Assert.Equal(0, statistics.FileCount);
        Assert.Null(statistics.AverageSizeReductionPercent);
        Assert.Equal(0, statistics.AverageInputSizeBytes);
        Assert.Equal(0, statistics.AverageOutputSizeBytes);
        Assert.Equal(TimeSpan.Zero, statistics.AverageProcessingTime);
    }

    [Fact]
    public void Create_WithNull_ReturnsEmpty()
    {
        var statistics = MediaConversionStatistics.Create(null);

        Assert.Equal(MediaConversionStatistics.Empty, statistics);
    }

    [Fact]
    public void Create_IgnoresNonCompletedResults()
    {
        var results = new[]
        {
            new MediaConversionResult(@"C:\a.mkv", @"C:\a.mkv", "Failed", 1000, 0, null, TimeSpan.FromSeconds(1)),
            new MediaConversionResult(@"C:\b.mkv", @"C:\b.mkv", MediaConversionResult.WhatIfStatus, 1000, 0, null, TimeSpan.Zero),
        };

        var statistics = MediaConversionStatistics.Create(results);

        Assert.Equal(MediaConversionStatistics.Empty, statistics);
    }

    [Fact]
    public void Create_AveragesCompletedResults()
    {
        var results = new[]
        {
            new MediaConversionResult(
                @"C:\a.mkv",
                @"C:\a.mp4",
                MediaConversionResult.CompletedStatus,
                1000,
                400,
                60.0,
                TimeSpan.FromSeconds(10)),
            new MediaConversionResult(
                @"C:\b.mkv",
                @"C:\b.mp4",
                MediaConversionResult.CompletedStatus,
                3000,
                1500,
                50.0,
                TimeSpan.FromSeconds(30)),
            new MediaConversionResult(
                @"C:\c.mkv",
                @"C:\c.mkv",
                "Failed",
                5000,
                0,
                null,
                TimeSpan.FromSeconds(5)),
        };

        var statistics = MediaConversionStatistics.Create(results);

        Assert.Equal(2, statistics.FileCount);
        Assert.Equal(55.0, statistics.AverageSizeReductionPercent);
        Assert.Equal(2000, statistics.AverageInputSizeBytes);
        Assert.Equal(950, statistics.AverageOutputSizeBytes);
        Assert.Equal(TimeSpan.FromSeconds(20), statistics.AverageProcessingTime);
    }
}
