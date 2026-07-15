using System;
using System.Collections.Generic;
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

        ffmpegMock
            .Setup(service => service.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, IEnumerable<string>?, IProgress<FfmpegProgress>?, CancellationToken>((_, _, _, progress, _) =>
            {
                callIndex++;
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
            "output.mp4",
            settings,
            [],
            progress: new SynchronousProgressReporter(reports.Add),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, callIndex);
        Assert.Equal(2, reports.Count);
        Assert.Equal(50, reports[0].PercentComplete);
        Assert.Equal(100, reports[1].PercentComplete);
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
                It.IsAny<CancellationToken>()))
            .Returns<string, string, IEnumerable<string>?, IProgress<FfmpegProgress>?, CancellationToken>((_, _, _, progress, _) =>
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
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class SynchronousProgressReporter(Action<FfmpegProgress> handler) : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value) => handler(value);
    }
}
