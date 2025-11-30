using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfprobeServiceTests
{
    private readonly Mock<IExecutableService> _executableServiceMock;
    private readonly Mock<ILogger<FfprobeService>> _loggerMock;
    private readonly FfprobeService _ffprobeService;

    public FfprobeServiceTests()
    {
        _executableServiceMock = new Mock<IExecutableService>();
        _loggerMock = new Mock<ILogger<FfprobeService>>();
        _ffprobeService = new FfprobeService(_executableServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Execute_WithSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var path = "test.mkv";
        var arguments = new[] { "-show_format" };
        var output = "{\"format\": {}}";
        var executableResult = new ExecutableResult(output, null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        var result = await _ffprobeService.Execute(path, arguments);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(output, result.Json);
    }

    [Fact]
    public async Task Execute_WithFailure_ReturnsFailureResult()
    {
        // Arrange
        var path = "test.mkv";
        var arguments = new[] { "-show_format" };
        var errorOutput = "Error: File not found";
        var executableResult = new ExecutableResult(null, errorOutput, 1);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        var result = await _ffprobeService.Execute(path, arguments);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.Json);
    }

    [Fact]
    public async Task Execute_WithNullOutput_ReturnsEmptyJson()
    {
        // Arrange
        var path = "test.mkv";
        var arguments = new[] { "-show_format" };
        var executableResult = new ExecutableResult(null, null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        var result = await _ffprobeService.Execute(path, arguments);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Json);
    }

    [Fact]
    public async Task Execute_AddsDefaultArguments()
    {
        // Arrange
        var path = "test.mkv";
        var arguments = new[] { "-show_format" };
        var executableResult = new ExecutableResult("{}", null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        await _ffprobeService.Execute(path, arguments);

        // Assert
        _executableServiceMock.Verify(e => e.Execute(
            "ffprobe",
            It.Is<IEnumerable<string>>(args => args.Contains("-v") && args.Contains("error") && args.Contains("-of") && args.Contains("json"))),
            Times.Once);
    }

    [Fact]
    public async Task Execute_AddsInputPathArgument()
    {
        // Arrange
        var path = "test.mkv";
        var arguments = new[] { "-show_format" };
        var executableResult = new ExecutableResult("{}", null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act
        await _ffprobeService.Execute(path, arguments);

        // Assert
        _executableServiceMock.Verify(e => e.Execute(
            "ffprobe",
            It.Is<IEnumerable<string>>(args => args.Contains("-i") && args.Contains(path)),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WithNullArguments_DoesNotThrow()
    {
        // Arrange
        var path = "test.mkv";
        var executableResult = new ExecutableResult("{}", null, 0);
        _executableServiceMock.Setup(e => e.Execute(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(executableResult);

        // Act & Assert
        await _ffprobeService.Execute(path, null!);
    }
}
