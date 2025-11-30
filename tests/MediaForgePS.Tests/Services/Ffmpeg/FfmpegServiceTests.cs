using System.Linq;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfmpegServiceTests
{
    private readonly Mock<IExecutableService> _executableServiceMock;
    private readonly Mock<ILogger<FfmpegService>> _loggerMock;
    private readonly FfmpegService _ffmpegService;

    public FfmpegServiceTests()
    {
        _executableServiceMock = new Mock<IExecutableService>();
        _loggerMock = new Mock<ILogger<FfmpegService>>();
        _ffmpegService = new FfmpegService(_executableServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ConvertAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var inputPath = "input.mkv";
        var outputPath = "output.mkv";
        var arguments = new[] { "-c:v", "libx264" };
        var executableResult = new ExecutableResult("", null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        var result = await _ffmpegService.ConvertAsync(inputPath, outputPath, arguments);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ConvertAsync_WithFailure_ReturnsFalse()
    {
        // Arrange
        var inputPath = "input.mkv";
        var outputPath = "output.mkv";
        var arguments = new[] { "-c:v", "libx264" };
        var executableResult = new ExecutableResult("", "Error", 1);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        var result = await _ffmpegService.ConvertAsync(inputPath, outputPath, arguments);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConvertAsync_AddsInputAndOutputPaths()
    {
        // Arrange
        var inputPath = "input.mkv";
        var outputPath = "output.mkv";
        var arguments = new[] { "-c:v", "libx264" };
        var executableResult = new ExecutableResult("", null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        await _ffmpegService.ConvertAsync(inputPath, outputPath, arguments);

        // Assert
        _executableServiceMock.Verify(e => e.Execute(
            "ffmpeg",
            It.Is<IEnumerable<string>>(args => args.Contains("-i") && args.Contains(inputPath) && args.Contains(outputPath)),
            Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_AddsOverwriteFlag()
    {
        // Arrange
        var inputPath = "input.mkv";
        var outputPath = "output.mkv";
        var arguments = new[] { "-c:v", "libx264" };
        var executableResult = new ExecutableResult("", null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        await _ffmpegService.ConvertAsync(inputPath, outputPath, arguments);

        // Assert
        _executableServiceMock.Verify(e => e.Execute(
            "ffmpeg",
            It.Is<IEnumerable<string>>(args => args.Contains("-y")),
            Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_WithNullArguments_DoesNotThrow()
    {
        // Arrange
        var inputPath = "input.mkv";
        var outputPath = "output.mkv";
        var executableResult = new ExecutableResult("", null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act & Assert
        var result = await _ffmpegService.ConvertAsync(inputPath, outputPath, null);
        Assert.True(result);
    }

    [Fact]
    public async Task ConvertAsync_IncludesCustomArguments()
    {
        // Arrange
        var inputPath = "input.mkv";
        var outputPath = "output.mkv";
        var arguments = new[] { "-c:v", "libx264", "-preset", "slow" };
        var executableResult = new ExecutableResult("", null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        await _ffmpegService.ConvertAsync(inputPath, outputPath, arguments);

        // Assert
        _executableServiceMock.Verify(e => e.Execute(
            "ffmpeg",
            It.Is<IEnumerable<string>>(args => args.Contains("-c:v") && args.Contains("libx264") && args.Contains("-preset") && args.Contains("slow")),
            Times.Once);
    }
}
