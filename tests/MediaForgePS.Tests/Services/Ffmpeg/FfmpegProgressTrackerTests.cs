using System;
using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfmpegProgressTrackerTests
{
    [Fact]
    public void HandleLine_WithOutTimeAndProgress_ReportsPercentAndEta()
    {
        var reports = new List<FfmpegProgress>();
        var tracker = new FfmpegProgressTracker(
            TimeSpan.FromSeconds(100),
            new SynchronousProgressReporter(reports.Add));

        tracker.HandleLine("out_time=00:00:25.000000");
        tracker.HandleLine("progress=continue");

        var report = Assert.Single(reports);
        Assert.Equal(TimeSpan.FromSeconds(25), report.OutTime);
        Assert.Equal(TimeSpan.FromSeconds(100), report.TotalDuration);
        Assert.Equal(25, report.PercentComplete);
        Assert.NotNull(report.EstimatedTimeRemaining);
    }

    [Fact]
    public void HandleLine_WithOutTimeUs_ParsesMicroseconds()
    {
        var reports = new List<FfmpegProgress>();
        var tracker = new FfmpegProgressTracker(
            TimeSpan.FromSeconds(10),
            new SynchronousProgressReporter(reports.Add));

        tracker.HandleLine("out_time_us=5000000");
        tracker.HandleLine("progress=continue");

        var report = Assert.Single(reports);
        Assert.Equal(TimeSpan.FromSeconds(5), report.OutTime);
        Assert.Equal(50, report.PercentComplete);
    }

    [Fact]
    public void HandleLine_WithProgressEnd_ReportsComplete()
    {
        var reports = new List<FfmpegProgress>();
        var tracker = new FfmpegProgressTracker(
            TimeSpan.FromSeconds(60),
            new SynchronousProgressReporter(reports.Add));

        tracker.HandleLine("out_time=00:00:30.000000");
        tracker.HandleLine("progress=end");

        var report = Assert.Single(reports);
        Assert.Equal(TimeSpan.FromSeconds(60), report.OutTime);
        Assert.Equal(100, report.PercentComplete);
        Assert.Equal(TimeSpan.Zero, report.EstimatedTimeRemaining);
    }

    [Fact]
    public void HandleLine_WithInvalidOutTime_IgnoresValue()
    {
        var reports = new List<FfmpegProgress>();
        var tracker = new FfmpegProgressTracker(
            TimeSpan.FromSeconds(10),
            new SynchronousProgressReporter(reports.Add));

        tracker.HandleLine("out_time=N/A");
        tracker.HandleLine("progress=continue");

        var report = Assert.Single(reports);
        Assert.Equal(TimeSpan.Zero, report.OutTime);
        Assert.Equal(0, report.PercentComplete);
        Assert.Null(report.EstimatedTimeRemaining);
    }

    [Fact]
    public void HandleLine_WithZeroDuration_ReportsZeroPercentUntilEnd()
    {
        var reports = new List<FfmpegProgress>();
        var tracker = new FfmpegProgressTracker(
            TimeSpan.Zero,
            new SynchronousProgressReporter(reports.Add));

        tracker.HandleLine("out_time=00:00:05.000000");
        tracker.HandleLine("progress=continue");
        tracker.HandleLine("progress=end");

        Assert.Equal(2, reports.Count);
        Assert.Equal(0, reports[0].PercentComplete);
        Assert.Null(reports[0].EstimatedTimeRemaining);
        Assert.Equal(100, reports[1].PercentComplete);
        Assert.Equal(TimeSpan.Zero, reports[1].EstimatedTimeRemaining);
    }

    [Fact]
    public void HandleLine_WithOutTimeBeyondDuration_ClampsToTotal()
    {
        var reports = new List<FfmpegProgress>();
        var tracker = new FfmpegProgressTracker(
            TimeSpan.FromSeconds(10),
            new SynchronousProgressReporter(reports.Add));

        tracker.HandleLine("out_time=00:00:15.000000");
        tracker.HandleLine("progress=continue");

        var report = Assert.Single(reports);
        Assert.Equal(TimeSpan.FromSeconds(10), report.OutTime);
        Assert.Equal(100, report.PercentComplete);
        Assert.Null(report.EstimatedTimeRemaining);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("frame")]
    [InlineData("=1")]
    [InlineData("speed=1.5x")]
    public void HandleLine_WithBlankOrNonProgressLines_DoesNotReport(string? line)
    {
        var reports = new List<FfmpegProgress>();
        var tracker = new FfmpegProgressTracker(
            TimeSpan.FromSeconds(10),
            new SynchronousProgressReporter(reports.Add));

        tracker.HandleLine(line!);

        Assert.Empty(reports);
    }

    [Fact]
    public void HandleLine_WithZeroOutTime_DoesNotEstimateRemaining()
    {
        var reports = new List<FfmpegProgress>();
        var tracker = new FfmpegProgressTracker(
            TimeSpan.FromSeconds(100),
            new SynchronousProgressReporter(reports.Add));

        tracker.HandleLine("out_time=00:00:00.000000");
        tracker.HandleLine("progress=continue");

        var report = Assert.Single(reports);
        Assert.Equal(0, report.PercentComplete);
        Assert.Null(report.EstimatedTimeRemaining);
    }

    private sealed class SynchronousProgressReporter(Action<FfmpegProgress> handler) : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value) => handler(value);
    }
}
