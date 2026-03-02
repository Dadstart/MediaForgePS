using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class GetMediaFileCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock;
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<GetMediaFileCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public GetMediaFileCommandTests()
    {
        _pathResolverMock = new Mock<IPathResolver>();
        _mediaReaderServiceMock = new Mock<IMediaReaderService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<GetMediaFileCommand>>();
        _debuggerServiceMock = new Mock<IDebuggerService>();

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_mediaReaderServiceMock.Object);
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void GetMediaFile_WhenPathNotResolved_WritesError()
    {
        var inputPath = "C:\\missing.mkv";
        string? resolvedPath = null;
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Get-MediaFile")
            .AddParameter("Path", inputPath);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        _mediaReaderServiceMock.Verify(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetMediaFile_WhenMediaReadFails_WritesError()
    {
        var inputPath = "C:\\test.mkv";
        var resolvedPath = "C:\\test.mkv";
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(true);
        _mediaReaderServiceMock.Setup(m => m.GetMediaFileAsync(resolvedPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ffprobe failed"));

        using var ps = CreatePowerShell();
        ps.AddCommand("Get-MediaFile")
            .AddParameter("Path", inputPath);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void GetMediaFile_WithValidPath_ReturnsMediaFile()
    {
        var inputPath = "C:\\test.mkv";
        var resolvedPath = "C:\\test.mkv";
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(true);

        var mediaFile = new MediaFile(
            resolvedPath,
            new MediaFormat(resolvedPath, 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            Array.Empty<MediaStream>(),
            "{}");

        _mediaReaderServiceMock.Setup(m => m.GetMediaFileAsync(resolvedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mediaFile);

        using var ps = CreatePowerShell();
        ps.AddCommand("Get-MediaFile")
            .AddParameter("Path", inputPath);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Single(results);
        Assert.Same(mediaFile, results[0].BaseObject);
        _mediaReaderServiceMock.Verify(m => m.GetMediaFileAsync(resolvedPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PowerShell CreatePowerShell()
    {
        return PowerShellCmdletTestHost.Create<GetMediaFileCommand>("Get-MediaFile");
    }
}
