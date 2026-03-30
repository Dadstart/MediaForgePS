using System;
using System.Collections.Generic;
using System.IO;
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

public sealed class ConvertMkvDirectoryCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock = new();
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock = new();
    private readonly Mock<IAudioTrackMappingService> _audioTrackMappingServiceMock = new();
    private readonly Mock<IMediaConversionService> _mediaConversionServiceMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<ConvertMkvDirectoryCommand>> _loggerMock = new();
    private readonly Mock<IDebuggerService> _debuggerServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public ConvertMkvDirectoryCommandTests()
    {
        _loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(debugger => debugger.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_mediaReaderServiceMock.Object);
        services.AddSingleton(_audioTrackMappingServiceMock.Object);
        services.AddSingleton(_mediaConversionServiceMock.Object);
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
    public void ConvertMkvDirectory_WithoutRecurse_ConvertsOnlyTopLevelMkvFiles()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();

        var firstMkv = Path.Combine(root, "one.mkv");
        var secondMkv = Path.Combine(root, "two.mkv");
        var subDirectory = Path.Combine(root, "sub");
        Directory.CreateDirectory(subDirectory);
        var nestedMkv = Path.Combine(subDirectory, "nested.mkv");

        File.WriteAllText(firstMkv, "x");
        File.WriteAllText(secondMkv, "x");
        File.WriteAllText(nestedMkv, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);

        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(firstMkv, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(firstMkv));
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(secondMkv, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(secondMkv));

        var firstOutput = Path.Combine(output, "one.mp4");
        var secondOutput = Path.Combine(output, "two.mp4");
        SetupOutputPathResolution(firstOutput);
        SetupOutputPathResolution(secondOutput);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MkvDirectory")
            .AddParameter("InputDirectory", root)
            .AddParameter("OutputDirectory", output);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            firstMkv,
            firstOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>()), Times.Once);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            secondMkv,
            secondOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>()), Times.Once);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            nestedMkv,
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>()), Times.Never);
    }

    [Fact]
    public void ConvertMkvDirectory_WithRecurse_ConvertsNestedMkvFiles()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var subDirectory = Path.Combine(root, "sub");
        Directory.CreateDirectory(subDirectory);

        var nestedMkv = Path.Combine(subDirectory, "nested.mkv");
        File.WriteAllText(nestedMkv, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(nestedMkv, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(nestedMkv));

        var expectedOutput = Path.Combine(output, "sub", "nested.mp4");
        SetupOutputPathResolution(expectedOutput);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MkvDirectory")
            .AddParameter("InputDirectory", root)
            .AddParameter("OutputDirectory", output)
            .AddParameter("Recurse");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            nestedMkv,
            expectedOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>()), Times.Once);
    }

    private void SetupOutputPathResolution(string outputPath)
    {
        var resolved = outputPath;
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveOutputPath(outputPath, out resolved))
            .Returns(true);
    }

    private static MediaFile CreateMediaFile(string path)
    {
        var stream = new MediaStream(
            "audio",
            1,
            "aac",
            string.Empty,
            string.Empty,
            new Dictionary<string, string> { ["language"] = "eng" },
            TimeSpan.Zero,
            "eng",
            @"{""index"":1,""codec_type"":""audio"",""channels"":2}");

        return new MediaFile(
            path,
            new MediaFormat(path, 2, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                new MediaStream("video", 0, "h264", string.Empty, string.Empty, new Dictionary<string, string>(), TimeSpan.Zero, null, @"{""index"":0,""codec_type"":""video""}"),
                stream
            },
            "{}");
    }

    private static PowerShell CreatePowerShell() => PowerShellCmdletTestHost.Create<ConvertMkvDirectoryCommand>("Convert-MkvDirectory");

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"MediaForgePS-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
