using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

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

        var moduleServicesType = typeof(ModuleServices);
        _providerField = moduleServicesType.GetField("_provider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        _initializedField = moduleServicesType.GetField("_initialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        InjectServiceProvider();
    }

    public void Dispose()
    {
        if (_providerField != null)
            _providerField.SetValue(null, null);
        if (_initializedField != null)
            _initializedField.SetValue(null, false);
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

    private void InjectServiceProvider()
    {
        if (_providerField != null)
            _providerField.SetValue(null, _serviceProvider);
        if (_initializedField != null)
            _initializedField.SetValue(null, true);
    }

    private static PowerShell CreatePowerShell()
    {
        var asm = typeof(GetMediaFileCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-MediaFile", typeof(GetMediaFileCommand), null));
        return PowerShell.Create(initialSessionState);
    }
}
