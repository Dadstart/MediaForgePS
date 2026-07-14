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

public class FfmpegServiceConvertResultTests
{
    [Fact]
    public async Task ConvertAsync_PassesInputOutputWithSpacesAndExtraArgsAsDistinctArgvEntries()
    {
        var inputPath = @"C:\Media Library\input show.mkv";
        var outputPath = @"D:\Exports\final cut.mp4";
        var extraArgs = new[] { "-c:v", "libx264", "-crf", "23" };
        string[]? capturedArgs = null;

        var executableMock = new Mock<IExecutableService>();
        executableMock
            .Setup(service => service.ExecuteAsync(
                "ffmpeg",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IEnumerable<string> args, CancellationToken _) =>
            {
                capturedArgs = args.ToArray();
                return new ExecutableResult(string.Empty, string.Empty, 0);
            });

        var service = CreateService(executableMock.Object);

        var result = await service.ConvertAsync(
            inputPath,
            outputPath,
            extraArgs,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.NotNull(capturedArgs);
        Assert.Equal(
            [
                "-i",
                inputPath,
                "-c:v",
                "libx264",
                "-crf",
                "23",
                "-y",
                outputPath
            ],
            capturedArgs);
        Assert.DoesNotContain(capturedArgs, arg => arg.Contains("-progress", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConvertAsync_WithProgress_PassesSpacedPathsUnquotedInArgumentList()
    {
        var inputPath = @"C:\Shows\Breaking Bad\S01E01.mkv";
        var outputPath = @"C:\Out\episode 01.mp4";
        string[]? capturedArgs = null;

        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();
        ffprobeMock
            .Setup(service => service.ExecuteAsync(inputPath, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FfprobeResult(true, """{"format":{"duration":"1.0"}}"""));

        executableMock
            .Setup(service => service.ExecuteAsync(
                "ffmpeg",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IEnumerable<string> args, Action<string> _, CancellationToken _) =>
            {
                capturedArgs = args.ToArray();
                return new ExecutableResult(string.Empty, string.Empty, 0);
            });

        var service = new FfmpegService(
            executableMock.Object,
            ffprobeMock.Object,
            NullLogger<FfmpegService>.Instance);

        await service.ConvertAsync(
            inputPath,
            outputPath,
            progress: new NoopProgress(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(capturedArgs);
        Assert.Contains("-nostats", capturedArgs);
        Assert.Contains("-progress", capturedArgs);
        Assert.Contains("pipe:1", capturedArgs);
        Assert.Equal(inputPath, capturedArgs[Array.IndexOf(capturedArgs, "-i") + 1]);
        Assert.Equal(outputPath, capturedArgs[^1]);
        Assert.DoesNotContain(capturedArgs, arg => arg.StartsWith('\"') || arg.EndsWith('\"'));
    }

    [Fact]
    public async Task ConvertAsync_WhenExecutableReturnsNonZeroExit_ThrowsFfmpegConversionException()
    {
        var executableMock = new Mock<IExecutableService>();
        executableMock
            .Setup(service => service.ExecuteAsync(
                "ffmpeg",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, "Conversion failed: invalid codec", 1));

        var service = CreateService(executableMock.Object);

        var ex = await Assert.ThrowsAsync<FfmpegConversionException>(() =>
            service.ConvertAsync(
                "input.mkv",
                "output.mp4",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("input.mkv", ex.InputPath);
        Assert.Equal("output.mp4", ex.OutputPath);
        Assert.Equal(1, ex.ExitCode);
        Assert.Equal("Conversion failed: invalid codec", ex.ErrorOutput);
        Assert.Contains("Exit code: 1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Conversion failed: invalid codec", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task ConvertAsync_WhenExecutableReturnsException_ThrowsFfmpegConversionExceptionWithInner()
    {
        var inner = new InvalidOperationException("failed to start process");
        var executableMock = new Mock<IExecutableService>();
        executableMock
            .Setup(service => service.ExecuteAsync(
                "ffmpeg",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(null, null, null, inner));

        var service = CreateService(executableMock.Object);

        var ex = await Assert.ThrowsAsync<FfmpegConversionException>(() =>
            service.ConvertAsync(
                "in.mkv",
                "out.mp4",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(inner, ex.InnerException);
        Assert.Equal("in.mkv", ex.InputPath);
        Assert.Equal("out.mp4", ex.OutputPath);
        Assert.Null(ex.ExitCode);
        Assert.Contains("failed to start process", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_WhenStreamingExecutableReturnsNonZeroExit_ThrowsFfmpegConversionException()
    {
        var executableMock = new Mock<IExecutableService>();
        var ffprobeMock = new Mock<IFfprobeService>();
        ffprobeMock
            .Setup(service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FfprobeResult(true, """{"format":{"duration":"2"}}"""));

        executableMock
            .Setup(service => service.ExecuteAsync(
                "ffmpeg",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, "encode aborted", 255));

        var service = new FfmpegService(
            executableMock.Object,
            ffprobeMock.Object,
            NullLogger<FfmpegService>.Instance);

        var ex = await Assert.ThrowsAsync<FfmpegConversionException>(() =>
            service.ConvertAsync(
                "input.mkv",
                "output.mp4",
                progress: new NoopProgress(),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(255, ex.ExitCode);
        Assert.Equal("encode aborted", ex.ErrorOutput);
    }

    private static FfmpegService CreateService(IExecutableService executable) =>
        new(executable, Mock.Of<IFfprobeService>(), NullLogger<FfmpegService>.Instance);

    private sealed class NoopProgress : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value)
        {
        }
    }
}
