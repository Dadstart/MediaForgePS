using System.Management.Automation;
using Dadstart.Labs.MediaForge.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Logging;

public class PowerShellCommandContextTests
{
    [Fact]
    public void GetCurrentContext_WhenNoContextSet_ReturnsNull()
    {
        // Arrange
        var context = new PowerShellCommandContext();

        // Act
        var result = context.GetCurrentContext();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SetCurrentContext_WithCmdlet_SetsContext()
    {
        // Arrange
        var context = new PowerShellCommandContext();
        var mockCmdlet = new Mock<PSCmdlet>();

        // Act
        context.SetCurrentContext(mockCmdlet.Object);
        var result = context.GetCurrentContext();

        // Assert
        Assert.Equal(mockCmdlet.Object, result);
    }

    [Fact]
    public void SetCurrentContext_WithNull_ClearsContext()
    {
        // Arrange
        var context = new PowerShellCommandContext();
        var mockCmdlet = new Mock<PSCmdlet>();
        context.SetCurrentContext(mockCmdlet.Object);

        // Act
        context.SetCurrentContext(null);
        var result = context.GetCurrentContext();

        // Assert
        Assert.Null(result);
    }
}

