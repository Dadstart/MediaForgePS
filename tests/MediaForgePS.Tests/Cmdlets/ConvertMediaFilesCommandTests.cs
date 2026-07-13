using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ConvertMediaFilesCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock = new();
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock = new();
    private readonly Mock<IMediaConversionService> _mediaConversionServiceMock = new();
    private readonly Mock<IAudioTrackMappingService> _audioTrackMappingServiceMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<ConvertMediaFilesCommand>> _loggerMock = new();
    private readonly Mock<IDebuggerService> _debuggerServiceMock = new();

    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public ConvertMediaFilesCommandTests()
    {
        _loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(debugger => debugger.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_mediaReaderServiceMock.Object);
        services.AddSingleton(_mediaConversionServiceMock.Object);
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
    public void ConvertMediaFiles_WhenInputPathCannotBeResolved_WritesErrorAndSkipsConversion()
    {
        var inputPath = CreateInputPath("missing.mkv");
        string? resolvedInputPath = null;
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", CreateOutputDirectory());

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
        _mediaReaderServiceMock.Verify(service => service.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>()), Times.Never);
    }

    [Fact]
    public void ConvertMediaFiles_WhenOutputPathCannotBeResolved_WritesErrorAndSkipsConversion()
    {
        var inputPath = CreateInputPath("episode1.mkv");
        var outputDirectory = CreateOutputDirectory();
        var expectedOutputPath = PathCombine(outputDirectory, "episode1.mp4");
        var resolvedInputPath = inputPath;
        string? unresolvedOutputPath = null;

        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveOutputPath(expectedOutputPath, out unresolvedOutputPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDirectory);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
        _mediaReaderServiceMock.Verify(service => service.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>()), Times.Never);
    }

    [Fact]
    public void ConvertMediaFiles_WithWindowsStyleInputPath_UsesFileNameForOutputPath()
    {
        var inputPath = @"C:\media\episode1.mkv";
        var outputDirectory = CreateOutputDirectory();
        var expectedOutputPath = PathCombine(outputDirectory, "episode1.mp4");
        var resolvedInputPath = inputPath;
        string? unresolvedOutputPath = null;

        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveOutputPath(expectedOutputPath, out unresolvedOutputPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDirectory);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
        _pathResolverMock.Verify(pathResolver => pathResolver.TryResolveOutputPath(expectedOutputPath, out unresolvedOutputPath), Times.Once);
    }

    [Fact]
    public void ConvertMediaFiles_WithProvidedAudioMappings_UsesProvidedMappingsAndSkipsAutoDetection()
    {
        var inputPath = CreateInputPath("episode1.mkv");
        var outputDirectory = CreateOutputDirectory();
        var resolvedInputPath = inputPath;
        var resolvedOutputPath = PathCombine(outputDirectory, "episode1.mp4");
        var providedMappings = new AudioTrackMapping[]
        {
            new CopyAudioTrackMapping("Custom", 0, 0, 0)
        };

        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveOutputPath(PathCombine(outputDirectory, "episode1.mp4"), out resolvedOutputPath))
            .Returns(true);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(resolvedInputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(resolvedInputPath, [CreateAudioStream(1, "aac", "eng", 2)]));

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDirectory)
            .AddParameter("AudioTrackMappings", providedMappings);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _audioTrackMappingServiceMock.Verify(service => service.CreateAutomaticMappings(It.IsAny<IEnumerable<MediaStream>>()), Times.Never);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            resolvedInputPath,
            resolvedOutputPath,
            It.IsAny<VideoEncodingSettings>(),
            It.Is<AudioTrackMapping[]>(mappings => mappings.Length == 1 && ReferenceEquals(mappings[0], providedMappings[0])),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>()), Times.Once);
    }

    [Fact]
    public void ConvertMediaFiles_WithoutProvidedAudioMappings_UsesAutoDetectedMappings()
    {
        var inputPath = CreateInputPath("episode1.mkv");
        var outputDirectory = CreateOutputDirectory();
        var resolvedInputPath = inputPath;
        var resolvedOutputPath = PathCombine(outputDirectory, "episode1.mp4");
        var autoMappings = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Auto AAC", 0, 0, 0, "aac", 192, 2)
        };

        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveOutputPath(PathCombine(outputDirectory, "episode1.mp4"), out resolvedOutputPath))
            .Returns(true);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(resolvedInputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(resolvedInputPath, [CreateAudioStream(1, "aac", "eng", 2)]));
        _audioTrackMappingServiceMock.Setup(service => service.CreateAutomaticMappings(It.IsAny<IEnumerable<MediaStream>>()))
            .Returns(autoMappings);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDirectory);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _audioTrackMappingServiceMock.Verify(service => service.CreateAutomaticMappings(It.IsAny<IEnumerable<MediaStream>>()), Times.Once);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            resolvedInputPath,
            resolvedOutputPath,
            It.IsAny<VideoEncodingSettings>(),
            It.Is<AudioTrackMapping[]>(mappings => mappings.Length == 1 && mappings[0].Title == "Auto AAC"),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>()), Times.Once);
    }

    [Fact]
    public void ConvertMediaFiles_WhenMediaReadReturnsNull_WritesWarningAndSkipsConversion()
    {
        var inputPath = CreateInputPath("episode1.mkv");
        var outputDirectory = CreateOutputDirectory();
        var resolvedInputPath = inputPath;
        var resolvedOutputPath = PathCombine(outputDirectory, "episode1.mp4");

        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveOutputPath(PathCombine(outputDirectory, "episode1.mp4"), out resolvedOutputPath))
            .Returns(true);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(resolvedInputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFile?)null);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDirectory);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();
        var warnings = ps.Streams.Warning.ReadAll();

        Assert.Empty(errors);
        Assert.NotEmpty(warnings);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>()), Times.Never);
    }

    [Fact]
    public void ConvertMediaFiles_WhenAutoDetectionThrows_WritesErrorAndSkipsConversion()
    {
        var inputPath = CreateInputPath("episode1.mkv");
        var outputDirectory = CreateOutputDirectory();
        var resolvedInputPath = inputPath;
        var resolvedOutputPath = PathCombine(outputDirectory, "episode1.mp4");

        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveOutputPath(PathCombine(outputDirectory, "episode1.mp4"), out resolvedOutputPath))
            .Returns(true);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(resolvedInputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(resolvedInputPath, [CreateAudioStream(1, "aac", "eng", 2)]));
        _audioTrackMappingServiceMock.Setup(service => service.CreateAutomaticMappings(It.IsAny<IEnumerable<MediaStream>>()))
            .Throws(new InvalidOperationException("No compatible audio layout"));

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDirectory);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();
        var warnings = ps.Streams.Warning.ReadAll();

        Assert.NotEmpty(errors);
        Assert.NotEmpty(warnings);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>()), Times.Never);
    }

    [Fact]
    public void ConvertMediaFiles_WithEncodeProgress_WritesPercentAndSecondsRemaining()
    {
        var inputPath = CreateInputPath("episode1.mkv");
        var outputDirectory = CreateOutputDirectory();
        var resolvedInputPath = inputPath;
        var resolvedOutputPath = PathCombine(outputDirectory, "episode1.mp4");
        var autoMappings = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Auto AAC", 0, 0, 0, "aac", 192, 2)
        };

        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(pathResolver => pathResolver.TryResolveOutputPath(PathCombine(outputDirectory, "episode1.mp4"), out resolvedOutputPath))
            .Returns(true);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(resolvedInputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(resolvedInputPath, [CreateAudioStream(1, "aac", "eng", 2)]));
        _audioTrackMappingServiceMock.Setup(service => service.CreateAutomaticMappings(It.IsAny<IEnumerable<MediaStream>>()))
            .Returns(autoMappings);

        _mediaConversionServiceMock
            .Setup(service => service.ExecuteConversion(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>()))
            .Callback((
                string _,
                string _,
                VideoEncodingSettings _,
                AudioTrackMapping[] _,
                string[]? _,
                IProgress<FfmpegProgress>? progress) =>
            {
                progress?.Report(new FfmpegProgress(
                    TimeSpan.FromSeconds(42),
                    TimeSpan.FromSeconds(100),
                    42,
                    TimeSpan.FromSeconds(30.2)));
                Thread.Sleep(200);
            });

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDirectory);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();
        var progress = ps.Streams.Progress.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(
            progress,
            record => record.Activity == "File Conversion"
                && record.PercentComplete == 42
                && record.SecondsRemaining == 31);
    }

    private static PowerShell CreatePowerShell() => PowerShellCmdletTestHost.Create<ConvertMediaFilesCommand>("Convert-MediaFiles");

    private static MediaFile CreateMediaFile(string path, MediaStream[] streams)
    {
        return new MediaFile(
            path,
            new MediaFormat(path, 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            streams,
            "{}");
    }

    private static MediaStream CreateAudioStream(int index, string codec, string language, int channels, string? title = null)
    {
        var tags = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(language))
            tags["language"] = language;
        if (!string.IsNullOrEmpty(title))
            tags["title"] = title;

        var rawJson = $@"{{
            ""index"": {index},
            ""codec_name"": ""{codec}"",
            ""codec_type"": ""audio"",
            ""channels"": {channels},
            ""tags"": {{}}
        }}";

        return new MediaStream(
            "audio",
            index,
            codec,
            string.Empty,
            string.Empty,
            tags,
            TimeSpan.Zero,
            language,
            rawJson);
    }

    private static string PathCombine(string outputDirectory, string fileName)
    {
        return Path.Combine(outputDirectory, fileName);
    }

    private static string CreateInputPath(string fileName)
    {
        var root = OperatingSystem.IsWindows() ? @"C:\media" : "/media";
        return Path.Combine(root, fileName);
    }

    private static string CreateOutputDirectory()
    {
        return OperatingSystem.IsWindows() ? @"C:\output" : "/output";
    }
}
