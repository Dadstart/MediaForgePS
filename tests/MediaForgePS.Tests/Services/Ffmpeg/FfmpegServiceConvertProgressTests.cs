using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfmpegServiceConvertProgressTests
{
    [Fact]
    public async Task ConvertAsync_WithProgress_ProbesDurationAddsProgressArgsAndReports()
    {
        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();
        var reports = new List<FfmpegProgress>();

        ffprobeMock
            .Setup(service => service.ExecuteAsync(
                "input.mkv",
                It.Is<IEnumerable<string>>(args => args.Contains("-show_entries") && args.Contains("format=duration")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FfprobeResult(true, """{"format":{"duration":"10.000000"}}"""));

        executableMock
            .Setup(service => service.ExecuteAsync(
                "ffmpeg",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, IEnumerable<string>, Action<string>, CancellationToken>((_, args, callback, _) =>
            {
                var argumentList = args.ToArray();
                Assert.Contains("-nostats", argumentList);
                Assert.Contains("-progress", argumentList);
                Assert.Contains("pipe:1", argumentList);
                Assert.Contains("-i", argumentList);
                Assert.Contains("input.mkv", argumentList);
                Assert.Contains("output.mp4", argumentList);

                callback("out_time=00:00:05.000000");
                callback("progress=continue");
                callback("out_time=00:00:10.000000");
                callback("progress=end");

                return Task.FromResult(new ExecutableResult(string.Empty, string.Empty, 0));
            });

        var service = new FfmpegService(
            executableMock.Object,
            ffprobeMock.Object,
            NullLogger<FfmpegService>.Instance);

        var result = await service.ConvertAsync(
            "input.mkv",
            "output.mp4",
            progress: new SynchronousProgressReporter(reports.Add),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(2, reports.Count);
        Assert.Equal(50, reports[0].PercentComplete);
        Assert.Equal(100, reports[1].PercentComplete);
        Assert.Equal(TimeSpan.FromSeconds(10), reports[0].TotalDuration);
        ffprobeMock.VerifyAll();
        executableMock.VerifyAll();
    }

    [Fact]
    public async Task ConvertAsync_WithoutProgress_DoesNotProbeOrStream()
    {
        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();

        executableMock
            .Setup(service => service.ExecuteAsync(
                "ffmpeg",
                It.Is<IEnumerable<string>>(args => !args.Contains("-progress")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, string.Empty, 0));

        var service = new FfmpegService(
            executableMock.Object,
            ffprobeMock.Object,
            NullLogger<FfmpegService>.Instance);

        var result = await service.ConvertAsync(
            "input.mkv",
            "output.mp4",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        ffprobeMock.Verify(
            service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        executableMock.Verify(
            service => service.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class SynchronousProgressReporter(Action<FfmpegProgress> handler) : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value) => handler(value);
    }
}
