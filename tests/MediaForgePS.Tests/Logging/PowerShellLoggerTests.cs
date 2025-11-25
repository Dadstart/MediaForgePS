using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Logging;

public class PowerShellLoggerTests
{
    private readonly Mock<IPowerShellCommandContextAccessor> _contextAccessorMock;
    private readonly Mock<PSCmdlet> _cmdletMock;
    private readonly PowerShellLogger _logger;

    public PowerShellLoggerTests()
    {
        _contextAccessorMock = new Mock<IPowerShellCommandContextAccessor>();
        _cmdletMock = new Mock<PSCmdlet>();
        _logger = new PowerShellLogger("TestCategory", _contextAccessorMock.Object);
    }

    [Fact]
    public void IsEnabled_WhenNoContext_ReturnsFalse()
    {
        // Arrange
        _contextAccessorMock.Setup(x => x.GetCurrentContext()).Returns((PSCmdlet?)null);

        // Act
        var result = _logger.IsEnabled(LogLevel.Information);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEnabled_WithNoneLevel_ReturnsFalse()
    {
        // Arrange
        _contextAccessorMock.Setup(x => x.GetCurrentContext()).Returns(_cmdletMock.Object);

        // Act
        var result = _logger.IsEnabled(LogLevel.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void BeginScope_ReturnsDisposable()
    {
        // Act
        var scope = _logger.BeginScope("Test scope");

        // Assert
        Assert.NotNull(scope);

        // Cleanup
        scope.Dispose();
    }

    [Fact]
    public void Log_WhenNoContext_DoesNotThrow()
    {
        // Arrange
        _contextAccessorMock.Setup(x => x.GetCurrentContext()).Returns((PSCmdlet?)null);

        // Act & Assert - should not throw when no context is available
        _logger.Log(LogLevel.Information, 0, "Test message", null, (state, exception) => state?.ToString() ?? string.Empty);
        _logger.Log(LogLevel.Warning, 0, "Test warning", null, (state, exception) => state?.ToString() ?? string.Empty);
        _logger.Log(LogLevel.Error, 0, "Test error", null, (state, exception) => state?.ToString() ?? string.Empty);
    }
}
