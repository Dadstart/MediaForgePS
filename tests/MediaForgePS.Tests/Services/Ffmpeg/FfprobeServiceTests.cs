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

public class FfprobeServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenExitCodeZero_ReturnsSuccessWithOutput()
    {
        const string path = @"C:\media\video.mkv";
        const string output = """{"format":{}}""";
        string[]? capturedArgs = null;
        TimeSpan? capturedTimeout = null;
        var executableMock = new Mock<IExecutableService>();

        executableMock.Setup(e => e.ExecuteAsync(
                "ffprobe",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()))
            .Callback<string, IEnumerable<string>, CancellationToken, TimeSpan?>((_, args, _, timeout) =>
            {
                capturedArgs = args.ToArray();
                capturedTimeout = timeout;
            })
            .ReturnsAsync(new ExecutableResult(output, string.Empty, 0));

        var service = new FfprobeService(executableMock.Object, NullLogger<FfprobeService>.Instance);
        var customArgs = new[] { "-show_format", "-show_streams" };

        var result = await service.ExecuteAsync(path, customArgs, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(output, result.Json);
        Assert.Equal(ProcessTimeouts.Probe, capturedTimeout);
        Assert.NotNull(capturedArgs);
        Assert.Equal(
            ["-v", "error", "-of", "json", "-show_format", "-show_streams", "-i", path],
            capturedArgs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExitCodeNonZero_ReturnsFailure()
    {
        const string path = @"C:\media\bad.mkv";
        var executableMock = new Mock<IExecutableService>();

        executableMock.Setup(e => e.ExecuteAsync(
                "ffprobe",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, "Invalid data", 1));

        var service = new FfprobeService(executableMock.Object, NullLogger<FfprobeService>.Instance);

        var result = await service.ExecuteAsync(path, ["-show_format"], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.Json);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExecutableThrows_ReturnsFailureWithEmptyJson()
    {
        const string path = @"C:\media\video.mkv";
        var executableMock = new Mock<IExecutableService>();

        executableMock.Setup(e => e.ExecuteAsync(
                "ffprobe",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync(new ExecutableResult(null, null, null, new InvalidOperationException("spawn failed")));

        var service = new FfprobeService(executableMock.Object, NullLogger<FfprobeService>.Instance);

        var result = await service.ExecuteAsync(path, ["-show_format"], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.Json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenPathIsNullOrWhitespace_Throws(string? path)
    {
        var service = new FfprobeService(Mock.Of<IExecutableService>(), NullLogger<FfprobeService>.Instance);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.ExecuteAsync(path!, ["-show_format"], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArgumentsNull_Throws()
    {
        var service = new FfprobeService(Mock.Of<IExecutableService>(), NullLogger<FfprobeService>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ExecuteAsync(@"C:\media\video.mkv", null!, TestContext.Current.CancellationToken));
    }
}
