using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.System;
using System.Management.Automation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class PathResolverTests
{
    private readonly Mock<ILogger<PathResolver>> _loggerMock;
    private readonly PathResolver _pathResolver;

    public PathResolverTests()
    {
        _loggerMock = new Mock<ILogger<PathResolver>>();
        _pathResolver = new PathResolver(_loggerMock.Object);
    }

    [Fact]
    public void TryResolveInputPath_WithNullCmdletContext_ReturnsFalse()
    {
        // Arrange
        CmdletContext.Current = null;

        // Act
        var result = _pathResolver.TryResolveInputPath("test.txt", out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(resolvedPath);
    }

    [Fact]
    public void TryResolveOutputPath_WithNullCmdletContext_ReturnsFalse()
    {
        // Arrange
        CmdletContext.Current = null;

        // Act
        var result = _pathResolver.TryResolveOutputPath("test.txt", out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(resolvedPath);
    }

    [Fact]
    public void TryResolveProviderPath_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var mockCmdlet = new Mock<PSCmdlet>();
        var testPath = "/test/path.txt";
        var resolvedPaths = new List<string> { testPath };
        mockCmdlet.Setup(c => c.GetResolvedProviderPathFromPSPath(It.IsAny<string>(), out It.Ref<ProviderInfo>.IsAny))
            .Returns(resolvedPaths);

        // Act
        var result = PathResolver.TryResolveProviderPath(mockCmdlet.Object, "test.txt", out var resolvedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(testPath, resolvedPath);
    }

    [Fact]
    public void TryResolveProviderPath_WithNoResolvedPaths_ReturnsFalse()
    {
        // Arrange
        var mockCmdlet = new Mock<PSCmdlet>();
        var resolvedPaths = new List<string>();
        mockCmdlet.Setup(c => c.GetResolvedProviderPathFromPSPath(It.IsAny<string>(), out It.Ref<ProviderInfo>.IsAny))
            .Returns(resolvedPaths);

        // Act
        var result = PathResolver.TryResolveProviderPath(mockCmdlet.Object, "test.txt", out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Null(resolvedPath);
    }

    [Fact]
    public void TryResolveProviderPath_WhenExceptionThrown_ReturnsFalse()
    {
        // Arrange
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Setup(c => c.GetResolvedProviderPathFromPSPath(It.IsAny<string>(), out It.Ref<ProviderInfo>.IsAny))
            .Throws<Exception>();

        // Act
        var result = PathResolver.TryResolveProviderPath(mockCmdlet.Object, "test.txt", out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Null(resolvedPath);
    }
}
