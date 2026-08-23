using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class MediaConversionServiceProgressTests
{
    [Fact]
    public void ExecuteConversion_TwoPass_MapsProgressAcrossPasses()
    {
        var ffmpegMock = new Mock<IFfmpegService>();
        var reports = new List<FfmpegProgress>();
        var callIndex = 0;
        var capturedOutputs = new List<string>();
        var capturedArgs = new List<string[]>();

        ffmpegMock
            .Setup(service => service.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>()))
            .Returns<string, string, IEnumerable<string>?, IProgress<FfmpegProgress>?, CancellationToken, TimeSpan?, bool>((_, outputPath, args, progress, _, __, ___) =>
            {
                callIndex++;
                capturedOutputs.Add(outputPath);
                capturedArgs.Add(args?.ToArray() ?? []);
                progress?.Report(new FfmpegProgress(
                    TimeSpan.FromSeconds(50),
                    TimeSpan.FromSeconds(100),
                    100,
                    TimeSpan.Zero));
                return Task.CompletedTask;
            });

        var service = new MediaConversionService(ffmpegMock.Object);
        var settings = new VariableRateVideoEncodingSettings(
            "libx264",
            "medium",
            "main",
            "film",
            2000,
            "yuv420p");

        service.ExecuteConversion(
            "input.mkv",
            Path.Combine(Path.GetTempPath(), "MediaForgePS-vbr-out.mp4"),
            settings,
            [],
            progress: new SynchronousProgressReporter(reports.Add),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, callIndex);
        Assert.Equal(2, reports.Count);
        Assert.Equal(50, reports[0].PercentComplete);
        Assert.Equal(100, reports[1].PercentComplete);
        Assert.True(AtomicFileHelper.IsNullMuxerOutput(capturedOutputs[0]));
        Assert.Contains("-pass", capturedArgs[0]);
        Assert.Contains("1", capturedArgs[0]);
        Assert.Contains("-an", capturedArgs[0]);
        Assert.Contains("-f", capturedArgs[0]);
        Assert.Contains("null", capturedArgs[0]);
        Assert.Contains("-pass", capturedArgs[1]);
        Assert.Contains("2", capturedArgs[1]);
        Assert.DoesNotContain("-an", capturedArgs[1]);

        var passLogIndex = Array.IndexOf(capturedArgs[0], "-passlogfile");
        Assert.True(passLogIndex >= 0);
        var passLogPath = capturedArgs[0][passLogIndex + 1];
        Assert.StartsWith(
            Path.Combine(Path.GetTempPath(), "MediaForgePS_"),
            Path.GetDirectoryName(passLogPath),
            StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.GetDirectoryName(passLogPath)));
    }

    [Fact]
    public void ExecuteConversion_SinglePass_ForwardsProgressUnchanged()
    {
        var ffmpegMock = new Mock<IFfmpegService>();
        var reports = new List<FfmpegProgress>();
        IProgress<FfmpegProgress>? capturedProgress = null;

        ffmpegMock
            .Setup(service => service.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>()))
            .Returns<string, string, IEnumerable<string>?, IProgress<FfmpegProgress>?, CancellationToken, TimeSpan?, bool>((_, _, _, progress, _, __, ___) =>
            {
                capturedProgress = progress;
                progress?.Report(new FfmpegProgress(
                    TimeSpan.FromSeconds(40),
                    TimeSpan.FromSeconds(100),
                    40,
                    TimeSpan.FromSeconds(90)));
                return Task.CompletedTask;
            });

        var service = new MediaConversionService(ffmpegMock.Object);
        var settings = new ConstantRateVideoEncodingSettings(
            "libx265",
            "medium",
            "main",
            "film",
            18,
            "yuv420p10le");
        var reporter = new SynchronousProgressReporter(reports.Add);

        service.ExecuteConversion(
            "input.mkv",
            "output.mp4",
            settings,
            [],
            progress: reporter,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(reporter, capturedProgress);
        var report = Assert.Single(reports);
        Assert.Equal(40, report.PercentComplete);
        Assert.Equal(TimeSpan.FromSeconds(90), report.EstimatedTimeRemaining);
        ffmpegMock.Verify(
            service => service.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>()),
            Times.Once);
    }

    private sealed class SynchronousProgressReporter(Action<FfmpegProgress> handler) : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value) => handler(value);
    }
}
