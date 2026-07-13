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

    private sealed class SynchronousProgressReporter(Action<FfmpegProgress> handler) : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value) => handler(value);
    }
}
