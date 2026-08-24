using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfmpegServiceConvertProgressTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_FfmpegProgress_" + Guid.NewGuid().ToString("N"));

    public FfmpegServiceConvertProgressTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ConvertAsync_WithProgress_ProbesDurationAddsProgressArgsAndReports()
    {
        var inputPath = Path.Combine(_tempDir, "input.mkv");
        var outputPath = Path.Combine(_tempDir, "output.mp4");
        File.WriteAllText(inputPath, "input");

        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();
        var reports = new List<FfmpegProgress>();

        ffprobeMock
            .Setup(service => service.ExecuteAsync(
                inputPath,
                It.Is<IEnumerable<string>>(args => args.Contains("-show_entries") && args.Contains("format=duration")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FfprobeResult(true, """{"format":{"duration":"10.000000"}}"""));

        executableMock
            .Setup(service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .Returns<string, IEnumerable<string>, Action<string>, CancellationToken, TimeSpan?>((_, args, callback, _, __) =>
            {
                var argumentList = args.ToArray();
                Assert.Contains("-nostats", argumentList);
                Assert.Contains("-progress", argumentList);
                Assert.Contains("pipe:1", argumentList);
                Assert.Contains("-i", argumentList);
                Assert.Contains(inputPath, argumentList);
                Assert.Contains(argumentList, arg =>
                    Path.GetDirectoryName(arg)?.StartsWith(
                        Path.Combine(Path.GetTempPath(), "MediaForgePS_"),
                        StringComparison.OrdinalIgnoreCase) == true);
                File.WriteAllText(argumentList[^1], "encoded");

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

        await service.ConvertAsync(
            inputPath,
            outputPath,
            progress: new SynchronousProgressReporter(reports.Add),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, reports.Count);
        Assert.Equal(50, reports[0].PercentComplete);
        Assert.Equal(100, reports[1].PercentComplete);
        Assert.Equal(TimeSpan.FromSeconds(10), reports[0].TotalDuration);
        Assert.True(File.Exists(outputPath));
        ffprobeMock.VerifyAll();
        executableMock.VerifyAll();
    }

    [Fact]
    public async Task ConvertAsync_WithProgressAndKnownDuration_DoesNotProbe()
    {
        var inputPath = Path.Combine(_tempDir, "input.mkv");
        var outputPath = Path.Combine(_tempDir, "output.mp4");
        File.WriteAllText(inputPath, "input");

        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();
        var reports = new List<FfmpegProgress>();

        executableMock
            .Setup(service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .Returns<string, IEnumerable<string>, Action<string>, CancellationToken, TimeSpan?>((_, args, callback, _, __) =>
            {
                var argumentList = args.ToArray();
                Assert.Contains("-progress", argumentList);
                File.WriteAllText(argumentList[^1], "encoded");

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

        await service.ConvertAsync(
            inputPath,
            outputPath,
            progress: new SynchronousProgressReporter(reports.Add),
            cancellationToken: TestContext.Current.CancellationToken,
            totalDuration: TimeSpan.FromSeconds(10));

        Assert.Equal(2, reports.Count);
        Assert.Equal(TimeSpan.FromSeconds(10), reports[0].TotalDuration);
        Assert.True(File.Exists(outputPath));
        ffprobeMock.Verify(
            service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        executableMock.VerifyAll();
    }

    [Fact]
    public async Task ConvertAsync_WithoutProgress_DoesNotProbeOrStream()
    {
        var inputPath = Path.Combine(_tempDir, "input.mkv");
        var outputPath = Path.Combine(_tempDir, "output.mp4");
        File.WriteAllText(inputPath, "input");

        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();

        executableMock
            .Setup(service => service.ExecuteAsync(
                "ffmpeg",
                It.Is<IEnumerable<string>>(args => !args.Contains("-progress")),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((string _, IEnumerable<string> args, CancellationToken _, TimeSpan? __) =>
            {
                File.WriteAllText(args.Last(), "encoded");
                return new ExecutableResult(string.Empty, string.Empty, 0);
            });

        var service = new FfmpegService(
            executableMock.Object,
            ffprobeMock.Object,
            NullLogger<FfmpegService>.Instance);

        await service.ConvertAsync(
            inputPath,
            outputPath,
            cancellationToken: TestContext.Current.CancellationToken);

        ffprobeMock.Verify(
            service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        executableMock.Verify(
            service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
        executableMock.Verify(
            service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Once);
        Assert.True(File.Exists(outputPath));
    }

    [Theory]
    [InlineData(false, "{}")]
    [InlineData(true, "")]
    [InlineData(true, "not-json")]
    [InlineData(true, """{"streams":[]}""")]
    [InlineData(true, """{"format":{}}""")]
    [InlineData(true, """{"format":{"duration":"0"}}""")]
    [InlineData(true, """{"format":{"duration":"-1"}}""")]
    [InlineData(true, """{"format":{"duration":"abc"}}""")]
    public async Task ConvertAsync_WithUnusableDuration_ConvertsWithZeroTotalDuration(bool probeSuccess, string json)
    {
        var inputPath = Path.Combine(_tempDir, $"input-{Guid.NewGuid():N}.mkv");
        var outputPath = Path.Combine(_tempDir, $"output-{Guid.NewGuid():N}.mp4");
        File.WriteAllText(inputPath, "input");

        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();
        var reports = new List<FfmpegProgress>();

        ffprobeMock
            .Setup(service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FfprobeResult(probeSuccess, json));

        executableMock
            .Setup(service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .Returns<string, IEnumerable<string>, Action<string>, CancellationToken, TimeSpan?>((_, args, callback, _, __) =>
            {
                File.WriteAllText(args.Last(), "encoded");
                callback("out_time=00:00:05.000000");
                callback("progress=continue");
                callback("progress=end");
                return Task.FromResult(new ExecutableResult(string.Empty, string.Empty, 0));
            });

        var service = new FfmpegService(
            executableMock.Object,
            ffprobeMock.Object,
            NullLogger<FfmpegService>.Instance);

        await service.ConvertAsync(
            inputPath,
            outputPath,
            progress: new SynchronousProgressReporter(reports.Add),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, reports.Count);
        Assert.Equal(TimeSpan.Zero, reports[0].TotalDuration);
        Assert.Equal(0, reports[0].PercentComplete);
        Assert.Equal(100, reports[1].PercentComplete);
    }

    [Fact]
    public async Task ConvertAsync_WithNumericDuration_ParsesSuccessfully()
    {
        var inputPath = Path.Combine(_tempDir, "input-numeric.mkv");
        var outputPath = Path.Combine(_tempDir, "output-numeric.mp4");
        File.WriteAllText(inputPath, "input");

        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();
        var reports = new List<FfmpegProgress>();

        ffprobeMock
            .Setup(service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FfprobeResult(true, """{"format":{"duration":20.5}}"""));

        executableMock
            .Setup(service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .Returns<string, IEnumerable<string>, Action<string>, CancellationToken, TimeSpan?>((_, args, callback, _, __) =>
            {
                File.WriteAllText(args.Last(), "encoded");
                callback("out_time=00:00:10.250000");
                callback("progress=continue");
                return Task.FromResult(new ExecutableResult(string.Empty, string.Empty, 0));
            });

        var service = new FfmpegService(
            executableMock.Object,
            ffprobeMock.Object,
            NullLogger<FfmpegService>.Instance);

        await service.ConvertAsync(
            inputPath,
            outputPath,
            progress: new SynchronousProgressReporter(reports.Add),
            cancellationToken: TestContext.Current.CancellationToken);

        var report = Assert.Single(reports);
        Assert.Equal(TimeSpan.FromSeconds(20.5), report.TotalDuration);
        Assert.Equal(50, report.PercentComplete);
    }

    private sealed class SynchronousProgressReporter(Action<FfmpegProgress> handler) : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value) => handler(value);
    }
}
