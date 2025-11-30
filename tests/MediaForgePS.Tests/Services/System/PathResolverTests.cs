using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class PathResolverTests : IDisposable
{
    private readonly Mock<ILogger<PathResolver>> _loggerMock;
    private readonly PathResolver _pathResolver;
    private readonly string _tempDirectory;

    public PathResolverTests()
    {
        _loggerMock = new Mock<ILogger<PathResolver>>();
        _pathResolver = new PathResolver(_loggerMock.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void TryResolveInputPath_WhenCmdletContextIsNull_ReturnsFalse()
    {
        // Arrange
        CmdletContext.Current = null;
        var path = "test.txt";

        // Act
        var result = _pathResolver.TryResolveInputPath(path, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(resolvedPath);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void TryResolveOutputPath_WhenCmdletContextIsNull_ReturnsFalse()
    {
        // Arrange
        CmdletContext.Current = null;
        var path = "output.txt";

        // Act
        var result = _pathResolver.TryResolveOutputPath(path, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(resolvedPath);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void TryResolveOutputPath_WhenPathResolutionFails_UsesPathAsIs()
    {
        // Arrange
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(new Collection<string>());
        CmdletContext.Current = mockCmdlet.Object;

        var path = Path.Combine(_tempDirectory, "newfile.txt");

        // Act
        var result = _pathResolver.TryResolveOutputPath(path, out var resolvedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(path, resolvedPath);
    }

    [Fact]
    public void TryResolveOutputPath_WhenDirectoryDoesNotExist_CreatesDirectory()
    {
        // Arrange
        var mockCmdlet = new Mock<PSCmdlet>();
        var newDirectory = Path.Combine(_tempDirectory, "subdir");
        var newFile = Path.Combine(newDirectory, "output.txt");

        var resolvedPaths = new Collection<string> { newFile };
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(resolvedPaths);
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        var result = _pathResolver.TryResolveOutputPath(newFile, out var resolvedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(newFile, resolvedPath);
        Assert.True(Directory.Exists(newDirectory));
    }

    [Fact]
    public void TryResolveOutputPath_WhenDirectoryExists_DoesNotThrow()
    {
        // Arrange
        var existingDirectory = Path.Combine(_tempDirectory, "existing");
        Directory.CreateDirectory(existingDirectory);
        var existingFile = Path.Combine(existingDirectory, "output.txt");

        var mockCmdlet = new Mock<PSCmdlet>();
        var resolvedPaths = new Collection<string> { existingFile };
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(resolvedPaths);
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        var result = _pathResolver.TryResolveOutputPath(existingFile, out var resolvedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(existingFile, resolvedPath);
        Assert.True(Directory.Exists(existingDirectory));
    }

    [Fact]
    public void TryResolveInputPath_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "test.txt");
        File.WriteAllText(testFile, "test content");

        var mockCmdlet = new Mock<PSCmdlet>();
        var resolvedPaths = new Collection<string> { testFile };
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(resolvedPaths);
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        var result = _pathResolver.TryResolveInputPath(testFile, out var resolvedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(testFile, resolvedPath);
    }

    [Fact]
    public void TryResolveInputPath_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.txt");

        var mockCmdlet = new Mock<PSCmdlet>();
        var resolvedPaths = new Collection<string> { nonExistentFile };
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(resolvedPaths);
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        var result = _pathResolver.TryResolveInputPath(nonExistentFile, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Equal(nonExistentFile, resolvedPath);
    }

    [Fact]
    public void TryResolveInputPath_WhenPathResolutionReturnsEmpty_ReturnsFalse()
    {
        // Arrange
        var path = "test.txt";
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(new Collection<string>());
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        var result = _pathResolver.TryResolveInputPath(path, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(resolvedPath);
    }

    [Fact]
    public void TryResolveInputPath_WhenPathResolutionThrows_ReturnsFalse()
    {
        // Arrange
        var path = "test.txt";
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Throws(new InvalidOperationException("Path resolution failed"));
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        var result = _pathResolver.TryResolveInputPath(path, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(resolvedPath);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void TryResolveOutputPath_WhenPathResolutionThrows_ReturnsFalse()
    {
        // Arrange
        var path = "output.txt";
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Throws(new InvalidOperationException("Path resolution failed"));
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        var result = _pathResolver.TryResolveOutputPath(path, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(resolvedPath);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void TryResolveInputPath_WhenResolvedPathEqualsInputAndFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var path = Path.Combine(_tempDirectory, "nonexistent.txt");
        var mockCmdlet = new Mock<PSCmdlet>();
        var resolvedPaths = new Collection<string> { path };
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(resolvedPaths);
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        var result = _pathResolver.TryResolveInputPath(path, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Equal(path, resolvedPath);
    }

    [Fact]
    public void TryResolveProviderPath_WhenGetResolvedProviderPathFromPSPathReturnsPaths_ReturnsTrueAndSetsResolvedPath()
    {
        // Arrange
        var path = "test.txt";
        var expectedResolvedPath = Path.Combine(_tempDirectory, "test.txt");
        var mockCmdlet = new Mock<PSCmdlet>();
        var resolvedPaths = new Collection<string> { expectedResolvedPath };
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(resolvedPaths);

        // Act
        var result = PathResolver.TryResolveProviderPath(mockCmdlet.Object, path, out var resolvedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedResolvedPath, resolvedPath);
    }

    [Fact]
    public void TryResolveProviderPath_WhenGetResolvedProviderPathFromPSPathReturnsEmptyCollection_ReturnsFalseAndSetsResolvedPathToNull()
    {
        // Arrange
        var path = "nonexistent.txt";
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(new Collection<string>());

        // Act
        var result = PathResolver.TryResolveProviderPath(mockCmdlet.Object, path, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Null(resolvedPath);
    }

    [Fact]
    public void TryResolveProviderPath_WhenGetResolvedProviderPathFromPSPathThrowsException_ReturnsFalseAndSetsResolvedPathToNull()
    {
        // Arrange
        var path = "test.txt";
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Throws(new InvalidOperationException("Path resolution failed"));

        // Act
        var result = PathResolver.TryResolveProviderPath(mockCmdlet.Object, path, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Null(resolvedPath);
    }

    [Fact]
    public void TryResolveProviderPath_WhenGetResolvedProviderPathFromPSPathReturnsMultiplePaths_ReturnsFirstPath()
    {
        // Arrange
        var path = "test.txt";
        var firstPath = Path.Combine(_tempDirectory, "test1.txt");
        var secondPath = Path.Combine(_tempDirectory, "test2.txt");
        var mockCmdlet = new Mock<PSCmdlet>();
        var resolvedPaths = new Collection<string> { firstPath, secondPath };
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Returns(resolvedPaths);

        // Act
        var result = PathResolver.TryResolveProviderPath(mockCmdlet.Object, path, out var resolvedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(firstPath, resolvedPath);
    }

    [Fact]
    public void TryResolveProviderPath_WhenPathIsNull_HandlesGracefully()
    {
        // Arrange
        string? path = null;
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Protected()
            .Setup<Collection<string>>("GetResolvedProviderPathFromPSPath", ItExpr.IsAny<string>(), ItExpr.Ref<ProviderInfo>.IsAny)
            .Throws(new ArgumentNullException(nameof(path)));

        // Act
        var result = PathResolver.TryResolveProviderPath(mockCmdlet.Object, path!, out var resolvedPath);

        // Assert
        Assert.False(result);
        Assert.Null(resolvedPath);
    }
}

