using Dadstart.Labs.MediaForge.Module;
using System.Management.Automation;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Module;

public class CmdletContextTests
{
    [Fact]
    public void Current_Initially_IsNull()
    {
        // Arrange & Act
        var current = CmdletContext.Current;

        // Assert
        Assert.Null(current);
    }

    [Fact]
    public void Current_SetValue_ReturnsSetValue()
    {
        // Arrange
        var mockCmdlet = new Mock<PSCmdlet>();

        // Act
        CmdletContext.Current = mockCmdlet.Object;
        var current = CmdletContext.Current;

        // Assert
        Assert.Same(mockCmdlet.Object, current);
    }

    [Fact]
    public void Current_SetNull_ClearsValue()
    {
        // Arrange
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        CmdletContext.Current = null;
        var current = CmdletContext.Current;

        // Assert
        Assert.Null(current);
    }

    [Fact]
    public void Current_SetDifferentValue_UpdatesValue()
    {
        // Arrange
        var mockCmdlet1 = new Mock<PSCmdlet>();
        var mockCmdlet2 = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet1.Object;

        // Act
        CmdletContext.Current = mockCmdlet2.Object;
        var current = CmdletContext.Current;

        // Assert
        Assert.Same(mockCmdlet2.Object, current);
        Assert.NotSame(mockCmdlet1.Object, current);
    }
}
