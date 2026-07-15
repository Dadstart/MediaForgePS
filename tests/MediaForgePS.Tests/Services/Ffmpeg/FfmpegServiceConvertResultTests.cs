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

public class FfmpegServiceConvertResultTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_FfmpegResult_" + Guid.NewGuid().ToString("N"));

    public FfmpegServiceConvertResultTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ConvertAsync_PassesInputAndExtraArgsAsDistinctArgvEntries_AndUsesTempOutput()
    {
        var inputPath = Path.Combine(_tempDir, "input show.mkv");
        var outputPath = Path.Combine(_tempDir, "final cut.mp4");
        File.WriteAllText(inputPath, "input");
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
                File.WriteAllText(capturedArgs[^1], "encoded");
                return new ExecutableResult(string.Empty, string.Empty, 0);
            });

        var service = CreateService(executableMock.Object);

        await service.ConvertAsync(
            inputPath,
            outputPath,
            extraArgs,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(capturedArgs);
        Assert.Equal("-i", capturedArgs[0]);
        Assert.Equal(inputPath, capturedArgs[1]);
        Assert.Contains("-c:v", capturedArgs);
        Assert.Contains("libx264", capturedArgs);
        Assert.Contains("-crf", capturedArgs);
        Assert.Contains("23", capturedArgs);
        Assert.Equal("-y", capturedArgs[^2]);
        Assert.EndsWith(".mediaforge.tmp." + Path.GetFileName(capturedArgs[^1]).Split(".mediaforge.tmp.")[1], capturedArgs[^1], StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath));
        Assert.Equal("encoded", File.ReadAllText(outputPath));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.mediaforge.tmp.*"));
    }

    [Fact]
    public async Task ConvertAsync_WithProgress_WritesToTempThenPromotes()
    {
        var inputPath = Path.Combine(_tempDir, "S01E01.mkv");
        var outputPath = Path.Combine(_tempDir, "episode 01.mp4");
        File.WriteAllText(inputPath, "input");
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
                File.WriteAllText(capturedArgs[^1], "encoded");
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
        Assert.Contains(".mediaforge.tmp.", capturedArgs[^1], StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task ConvertAsync_WhenExecutableReturnsNonZeroExit_DoesNotCreateFinalOutput()
    {
        var inputPath = Path.Combine(_tempDir, "input.mkv");
        var outputPath = Path.Combine(_tempDir, "output.mp4");
        File.WriteAllText(inputPath, "input");
        File.WriteAllText(outputPath, "existing");

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
                inputPath,
                outputPath,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(inputPath, ex.InputPath);
        Assert.Equal(outputPath, ex.OutputPath);
        Assert.Equal(1, ex.ExitCode);
        Assert.Equal("Conversion failed: invalid codec", ex.ErrorOutput);
        Assert.Equal("existing", File.ReadAllText(outputPath));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.mediaforge.tmp.*"));
    }

    [Fact]
    public async Task ConvertAsync_WhenExecutableReturnsException_ThrowsFfmpegConversionExceptionWithInner()
    {
        var inputPath = Path.Combine(_tempDir, "in.mkv");
        var outputPath = Path.Combine(_tempDir, "out.mp4");
        File.WriteAllText(inputPath, "input");

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
                inputPath,
                outputPath,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(inner, ex.InnerException);
        Assert.Equal(inputPath, ex.InputPath);
        Assert.Equal(outputPath, ex.OutputPath);
        Assert.Null(ex.ExitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.mediaforge.tmp.*"));
    }

    [Fact]
    public async Task ConvertAsync_NullMuxerOutput_DoesNotCreateTempSibling()
    {
        var inputPath = Path.Combine(_tempDir, "input.mkv");
        File.WriteAllText(inputPath, "input");
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
        await service.ConvertAsync(
            inputPath,
            "NUL",
            ["-f", "null"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(capturedArgs);
        Assert.Equal("NUL", capturedArgs[^1]);
        Assert.DoesNotContain(capturedArgs, arg => arg.Contains(".mediaforge.tmp.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConvertAsync_WhenStreamingExecutableReturnsNonZeroExit_ThrowsFfmpegConversionException()
    {
        var inputPath = Path.Combine(_tempDir, "input.mkv");
        var outputPath = Path.Combine(_tempDir, "output.mp4");
        File.WriteAllText(inputPath, "input");

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
                inputPath,
                outputPath,
                progress: new NoopProgress(),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(255, ex.ExitCode);
        Assert.Equal("encode aborted", ex.ErrorOutput);
        Assert.False(File.Exists(outputPath));
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
