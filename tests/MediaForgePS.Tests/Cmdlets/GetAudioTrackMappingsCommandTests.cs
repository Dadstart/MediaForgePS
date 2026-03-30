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

public class GetAudioTrackMappingsCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock;
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock;
    private readonly Mock<IAudioTrackMappingService> _audioTrackMappingServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<GetAudioTrackMappingsCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public GetAudioTrackMappingsCommandTests()
    {
        _pathResolverMock = new Mock<IPathResolver>();
        _mediaReaderServiceMock = new Mock<IMediaReaderService>();
        _audioTrackMappingServiceMock = new Mock<IAudioTrackMappingService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<GetAudioTrackMappingsCommand>>();
        _debuggerServiceMock = new Mock<IDebuggerService>();

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_mediaReaderServiceMock.Object);
        services.AddSingleton(_audioTrackMappingServiceMock.Object);
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
    public void GetAudioStreams_WhenInputPathNotResolved_WritesError()
    {
        var inputPath = "C:\\missing.mkv";
        string? resolvedPath = null;
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Get-AudioStreams")
            .AddParameter("InputPath", inputPath);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        _mediaReaderServiceMock.Verify(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _audioTrackMappingServiceMock.Verify(s => s.CreateMappings(It.IsAny<MediaFile>()), Times.Never);
    }

    [Fact]
    public void GetAudioStreams_WhenMediaFileReadReturnsNull_WritesError()
    {
        var inputPath = "C:\\test.mkv";
        var resolvedPath = "C:\\test.mkv";
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(true);
        _mediaReaderServiceMock.Setup(m => m.GetMediaFileAsync(resolvedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFile?)null);

        using var ps = CreatePowerShell();
        ps.AddCommand("Get-AudioStreams")
            .AddParameter("InputPath", inputPath);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        _audioTrackMappingServiceMock.Verify(s => s.CreateMappings(It.IsAny<MediaFile>()), Times.Never);
    }

    [Fact]
    public void GetAudioStreams_WithValidMediaFile_ReturnsMappings()
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

        var expectedMappings = new AudioTrackMapping[]
        {
            new CopyAudioTrackMapping("eng", 0, 0, 0)
        };

        _mediaReaderServiceMock.Setup(m => m.GetMediaFileAsync(resolvedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mediaFile);
        _audioTrackMappingServiceMock.Setup(s => s.CreateMappings(mediaFile))
            .Returns(expectedMappings);

        using var ps = CreatePowerShell();
        ps.AddCommand("Get-AudioStreams")
            .AddParameter("InputPath", inputPath);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Single(results);
        Assert.Same(expectedMappings, results[0].BaseObject);
        _mediaReaderServiceMock.Verify(m => m.GetMediaFileAsync(resolvedPath, It.IsAny<CancellationToken>()), Times.Once);
        _audioTrackMappingServiceMock.Verify(s => s.CreateMappings(mediaFile), Times.Once);
    }

    private static PowerShell CreatePowerShell()
    {
        return PowerShellCmdletTestHost.Create<GetAudioTrackMappingsCommand>("Get-AudioStreams");
    }
}
