using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class MediaConversionServiceProgressTests
{
    [Fact]
    public void ExecuteConversion_TwoPass_MapsProgressAcrossPasses()
    {
        var ffmpegMock = new Mock<IFfmpegService>();
        var platformMock = new Mock<IPlatformService>();
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
                return Task.FromResult(true);
            });

        var service = new MediaConversionService(ffmpegMock.Object, platformMock.Object);
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
            progress: new SynchronousProgressReporter(reports.Add));

        Assert.Equal(2, callIndex);
        Assert.Equal(2, reports.Count);
        Assert.Equal(50, reports[0].PercentComplete);
        Assert.Equal(100, reports[1].PercentComplete);
    }

    private sealed class SynchronousProgressReporter(Action<FfmpegProgress> handler) : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value) => handler(value);
    }
}
