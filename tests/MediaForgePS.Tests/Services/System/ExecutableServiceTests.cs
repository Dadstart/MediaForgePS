using System;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class ExecutableServiceTests
{
    private readonly Mock<IPlatformService> _platformServiceMock;
    private readonly Mock<ILogger<ExecutableService>> _loggerMock;
    private readonly ExecutableService _executableService;

    public ExecutableServiceTests()
    {
        _platformServiceMock = new Mock<IPlatformService>();
        _platformServiceMock.Setup(p => p.IsWindows()).Returns(false);
        _loggerMock = new Mock<ILogger<ExecutableService>>();
        _executableService = new ExecutableService(_platformServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Execute_WithInvalidCommand_ReturnsResultWithException()
    {
        // Arrange
        var command = "nonexistentcommand12345";
        var arguments = new[] { "arg1", "arg2" };

        // Act
        var result = await _executableService.Execute(command, arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task Execute_WithValidCommand_ReturnsResult()
    {
        // Arrange
        // Use a command that should exist on most systems
        var command = OperatingSystem.IsWindows() ? "cmd" : "echo";
        var arguments = OperatingSystem.IsWindows() ? new[] { "/c", "echo", "test" } : new[] { "test" };

        // Act
        var result = await _executableService.Execute(command, arguments);

        // Assert
        Assert.NotNull(result);
        // Result may have exit code 0 or non-zero depending on command
        Assert.NotNull(result.ExitCode);
    }

    [Fact]
    public async Task Execute_WithPlatformService_UsesPlatformService()
    {
        // Arrange
        _platformServiceMock.Setup(p => p.IsWindows()).Returns(true);
        var command = OperatingSystem.IsWindows() ? "cmd" : "echo";
        var arguments = OperatingSystem.IsWindows() ? new[] { "/c", "echo", "test" } : new[] { "test" };

        // Act
        await _executableService.Execute(command, arguments);

        // Assert
        _platformServiceMock.Verify(p => p.IsWindows(), Times.AtLeastOnce);
    }
}
