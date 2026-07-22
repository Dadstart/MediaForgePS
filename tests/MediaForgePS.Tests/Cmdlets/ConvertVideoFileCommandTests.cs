using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class ConvertVideoFileCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock = new();
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock = new();
    private readonly Mock<IAudioTrackMappingService> _audioTrackMappingServiceMock = new();
    private readonly Mock<IMediaConversionService> _mediaConversionServiceMock = new();
    private readonly Mock<IExecutableService> _executableServiceMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<ConvertVideoFileCommand>> _loggerMock = new();
    private readonly Mock<IDebuggerService> _debuggerServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public ConvertVideoFileCommandTests()
    {
        _loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(debugger => debugger.BreakIfDebugging(It.IsAny<bool>()));

        var ocrConverterMock = new Mock<IImageSubtitleOcrConverter>();
        ocrConverterMock.SetupGet(c => c.IsAvailable).Returns(true);
        ocrConverterMock.SetupGet(c => c.ExpectedTessDataDescription).Returns("tessdata expected");

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_mediaReaderServiceMock.Object);
        services.AddSingleton(_audioTrackMappingServiceMock.Object);
        services.AddSingleton(_mediaConversionServiceMock.Object);
        services.AddSingleton(_executableServiceMock.Object);
        services.AddSingleton<IImageSubtitleOcrConverter>(ocrConverterMock.Object);
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
    public void ConvertVideoFile_WithoutRecurse_ConvertsOnlyTopLevelMkvFiles()
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
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", root)
            .AddParameter("OutputDirectory", output);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            firstMkv,
            firstOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            secondMkv,
            secondOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            nestedMkv,
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ConvertVideoFile_WritesProgressAndHostStatusStreams()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();

        var firstMkv = Path.Combine(root, "one.mkv");
        var secondMkv = Path.Combine(root, "two.mkv");
        File.WriteAllText(firstMkv, "x");
        File.WriteAllText(secondMkv, "x");

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
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", root)
            .AddParameter("OutputDirectory", output);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);

        var progress = ps.Streams.Progress.ReadAll();
        Assert.NotEmpty(progress);
        Assert.Contains(
            progress,
            p => string.Equals(p.Activity, "Video file conversion", StringComparison.Ordinal));
        Assert.Contains(
            progress,
            p => string.Equals(p.Activity, "File conversion", StringComparison.Ordinal));
        Assert.Contains(
            progress,
            p => p.StatusDescription.Contains("File 1 of 2", StringComparison.Ordinal)
                || p.StatusDescription.Contains("File 2 of 2", StringComparison.Ordinal));

        var information = ps.Streams.Information.ReadAll();
        Assert.Contains(
            information,
            r => r.MessageData is HostInformationMessage host
                && host.Message.Contains("Converting 2 video file", StringComparison.Ordinal));
    }

    [Fact]
    public void ConvertVideoFile_WithEncodeProgress_WritesPercentAndSecondsRemaining()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(root, "one.mkv");
        File.WriteAllText(mkvPath, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));

        var expectedOutput = Path.Combine(output, "one.mp4");
        SetupOutputPathResolution(expectedOutput);

        _mediaConversionServiceMock
            .Setup(service => service.ExecuteConversion(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback((
                string _,
                string _,
                VideoEncodingSettings _,
                AudioTrackMapping[] _,
                string[]? _,
                IProgress<FfmpegProgress>? progress, CancellationToken _) =>
            {
                progress?.Report(new FfmpegProgress(
                    TimeSpan.FromSeconds(25),
                    TimeSpan.FromSeconds(100),
                    25,
                    TimeSpan.FromSeconds(12.1)));
                Thread.Sleep(200);
            });

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", mkvPath)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();
        var progressRecords = ps.Streams.Progress.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(
            progressRecords,
            record => record.Activity == "File conversion"
                && record.StatusDescription.Contains("00:25 / 01:40", StringComparison.Ordinal)
                && record.PercentComplete == 25
                && record.SecondsRemaining == 13);
    }

    [Fact]
    public void ConvertVideoFile_WithOneSecondRemaining_WritesFinishingSpinner()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(root, "one.mkv");
        File.WriteAllText(mkvPath, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));

        var expectedOutput = Path.Combine(output, "one.mp4");
        SetupOutputPathResolution(expectedOutput);

        _mediaConversionServiceMock
            .Setup(service => service.ExecuteConversion(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback((
                string _,
                string _,
                VideoEncodingSettings _,
                AudioTrackMapping[] _,
                string[]? _,
                IProgress<FfmpegProgress>? progress, CancellationToken _) =>
            {
                progress?.Report(new FfmpegProgress(
                    TimeSpan.FromSeconds(99),
                    TimeSpan.FromSeconds(100),
                    99,
                    TimeSpan.FromSeconds(1)));
                Thread.Sleep(200);
            });

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", mkvPath)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();
        var progressRecords = ps.Streams.Progress.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(
            progressRecords,
            record => record.Activity == "File conversion"
                && record.StatusDescription.StartsWith("finishing ", StringComparison.Ordinal)
                && record.PercentComplete == 99
                && record.SecondsRemaining == -1);
    }

    [Fact]
    public void ConvertVideoFile_WithRecurse_ConvertsNestedMkvFiles()
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
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", root)
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
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ConvertVideoFile_WithSingleMkvInput_ConvertsOnlySpecifiedFile()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(root, "one.mkv");
        var otherMkvPath = Path.Combine(root, "two.mkv");
        File.WriteAllText(mkvPath, "x");
        File.WriteAllText(otherMkvPath, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));

        var expectedOutput = Path.Combine(output, "one.mp4");
        SetupOutputPathResolution(expectedOutput);

        using var ps = CreatePowerShell("Convert-VideoFile");
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", mkvPath)
            .AddParameter("OutputDirectory", output);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            mkvPath,
            expectedOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            otherMkvPath,
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ConvertVideoFile_WritesConversionResultWithSizeReductionAndDuration()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(root, "one.mkv");
        File.WriteAllBytes(mkvPath, new byte[1000]);

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));

        var expectedOutput = Path.Combine(output, "one.mp4");
        SetupOutputPathResolution(expectedOutput);
        _mediaConversionServiceMock
            .Setup(service => service.ExecuteConversion(
                mkvPath,
                expectedOutput,
                It.IsAny<VideoEncodingSettings>(),
                mapping,
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => File.WriteAllBytes(expectedOutput, new byte[400]));

        using var ps = CreatePowerShell("Convert-VideoFile");
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", mkvPath)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<MediaConversionResult>(
            Assert.Single(results.Select(r => r.BaseObject).OfType<MediaConversionResult>()));
        Assert.Equal(MediaConversionResult.CompletedStatus, result.Status);
        Assert.Equal(mkvPath, result.InputPath);
        Assert.Equal(expectedOutput, result.OutputPath);
        Assert.Equal(MediaConversionHelper.BytesToMegabytes(1000), result.InputSizeMegabytes);
        Assert.Equal(MediaConversionHelper.BytesToMegabytes(400), result.OutputSizeMegabytes);
        Assert.Equal(60.0, result.SizeReductionPercent);
        Assert.True(result.ProcessingTime >= TimeSpan.Zero);

        Assert.Empty(results.Select(r => r.BaseObject).OfType<MediaConversionStatistics>());
    }

    [Fact]
    public void ConvertVideoFile_WhenConversionCancelled_DoesNotWriteFailedConversionResult()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(root, "one.mkv");
        File.WriteAllText(mkvPath, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));

        var expectedOutput = Path.Combine(output, "one.mp4");
        SetupOutputPathResolution(expectedOutput);
        _mediaConversionServiceMock
            .Setup(service => service.ExecuteConversion(
                mkvPath,
                expectedOutput,
                It.IsAny<VideoEncodingSettings>(),
                mapping,
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        using var ps = CreatePowerShell("Convert-VideoFile");
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", mkvPath)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        var results = ps.Invoke();
        var conversionResults = results
            .Select(r => r.BaseObject)
            .OfType<MediaConversionResult>()
            .ToList();

        // Cancellation must stop the pipeline — not become a failed MediaConversionResult.
        Assert.Empty(conversionResults);
    }

    [Fact]
    public void ConvertVideoFile_WithArrayOfMkvFiles_ConvertsOnlySpecifiedFiles()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var firstMkvPath = Path.Combine(root, "one.mkv");
        var secondMkvPath = Path.Combine(root, "two.mkv");
        var ignoredMkvPath = Path.Combine(root, "three.mkv");
        File.WriteAllText(firstMkvPath, "x");
        File.WriteAllText(secondMkvPath, "x");
        File.WriteAllText(ignoredMkvPath, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(firstMkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(firstMkvPath));
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(secondMkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(secondMkvPath));

        var firstOutput = Path.Combine(output, "one.mp4");
        var secondOutput = Path.Combine(output, "two.mp4");
        SetupOutputPathResolution(firstOutput);
        SetupOutputPathResolution(secondOutput);

        using var ps = CreatePowerShell("Convert-VideoFile");
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", new[] { firstMkvPath, secondMkvPath })
            .AddParameter("OutputDirectory", output);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            firstMkvPath,
            firstOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            secondMkvPath,
            secondOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            ignoredMkvPath,
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ConvertVideoFile_WithEnglishSubrip_ExtractsSubtitleBesideOutputMp4()
    {
        _executableServiceMock
            .Setup(service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, string.Empty, 0));

        var root = CreateTempDirectory();
        var output = CreateTempDirectory();

        var mkvPath = Path.Combine(root, "one.mkv");
        File.WriteAllText(mkvPath, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFileWithEnglishSubrip(mkvPath));

        var mp4Output = Path.Combine(output, "one.mp4");
        var srtOutput = Path.Combine(output, "one.eng.sdh.srt");
        SetupOutputPathResolution(mp4Output);
        SetupOutputPathResolution(srtOutput);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", root)
            .AddParameter("OutputDirectory", output);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _executableServiceMock.Verify(
            service => service.ExecuteAsync(
                "ffmpeg",
                It.Is<IEnumerable<string>>(args => args.Contains("-map") && args.Contains("0:2")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ConvertVideoFile_SkipSubtitles_DoesNotExtractSubtitles()
    {
        _executableServiceMock
            .Setup(service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, string.Empty, 0));

        var root = CreateTempDirectory();
        var output = CreateTempDirectory();

        var mkvPath = Path.Combine(root, "one.mkv");
        File.WriteAllText(mkvPath, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFileWithEnglishSubrip(mkvPath));

        var mp4Output = Path.Combine(output, "one.mp4");
        SetupOutputPathResolution(mp4Output);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", root)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _executableServiceMock.Verify(
            service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ConvertVideoFile_WithMp4SingleInput_ConvertsFile()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mp4Path = Path.Combine(root, "clip.mp4");
        File.WriteAllText(mp4Path, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mp4Path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mp4Path));

        var expectedOutput = Path.Combine(output, "clip.mp4");
        SetupOutputPathResolution(expectedOutput);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", mp4Path)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            mp4Path,
            expectedOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ConvertVideoFile_DirectoryWithMixedVideoExtensions_ConvertsAllSupportedFiles()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();

        var mkvPath = Path.Combine(root, "alpha.mkv");
        var mp4Path = Path.Combine(root, "bravo.mp4");
        var movPath = Path.Combine(root, "charlie.mov");
        var aviPath = Path.Combine(root, "delta.AVI");
        var textPath = Path.Combine(root, "notes.txt");

        File.WriteAllText(mkvPath, "x");
        File.WriteAllText(mp4Path, "x");
        File.WriteAllText(movPath, "x");
        File.WriteAllText(aviPath, "x");
        File.WriteAllText(textPath, "ignored");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);

        foreach (var path in new[] { mkvPath, mp4Path, movPath, aviPath })
        {
            _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(path, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMediaFile(path));
        }

        var mkvOutput = Path.Combine(output, "alpha.mp4");
        var mp4Output = Path.Combine(output, "bravo.mp4");
        var movOutput = Path.Combine(output, "charlie.mp4");
        var aviOutput = Path.Combine(output, "delta.mp4");
        SetupOutputPathResolution(mkvOutput);
        SetupOutputPathResolution(mp4Output);
        SetupOutputPathResolution(movOutput);
        SetupOutputPathResolution(aviOutput);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", root)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        foreach (var (input, expected) in new[]
        {
            (mkvPath, mkvOutput),
            (mp4Path, mp4Output),
            (movPath, movOutput),
            (aviPath, aviOutput),
        })
        {
            _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
                input,
                expected,
                It.IsAny<VideoEncodingSettings>(),
                mapping,
                It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            textPath,
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ConvertVideoFile_WithUnsupportedExtension_WritesErrorAndDoesNotConvert()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var unsupportedPath = Path.Combine(root, "notes.txt");
        File.WriteAllText(unsupportedPath, "x");

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", unsupportedPath)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        var error = Assert.Single(errors);
        Assert.Equal("InvalidInputPath", error.FullyQualifiedErrorId.Split(',')[0]);
        Assert.Contains("not a supported video format", error.Exception.Message, StringComparison.OrdinalIgnoreCase);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ConvertVideoFile_WithMixedSupportedAndUnsupportedFiles_ConvertsSupportedAndWritesErrorForUnsupported()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var unsupportedPath = Path.Combine(root, "notes.txt");
        var mkvPath = Path.Combine(root, "clip.mkv");
        File.WriteAllText(unsupportedPath, "x");
        File.WriteAllText(mkvPath, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));

        var expectedOutput = Path.Combine(output, "clip.mp4");
        SetupOutputPathResolution(expectedOutput);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", new[] { unsupportedPath, mkvPath })
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        var error = Assert.Single(errors);
        Assert.Equal("InvalidInputPath", error.FullyQualifiedErrorId.Split(',')[0]);
        Assert.Contains("not a supported video format", error.Exception.Message, StringComparison.OrdinalIgnoreCase);
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            mkvPath,
            expectedOutput,
            It.IsAny<VideoEncodingSettings>(),
            mapping,
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ConvertVideoFile_DirectoryWithOnlyNonVideoFiles_WritesNoSupportedVideoFilesWarning()
    {
        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "readme.txt"), "x");
        File.WriteAllText(Path.Combine(root, "image.png"), "x");

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", root)
            .AddParameter("OutputDirectory", output)
            .AddParameter("SkipSubtitles");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();
        var warnings = ps.Streams.Warning.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(warnings, w => w.Message.Contains("No supported video files", StringComparison.OrdinalIgnoreCase));
        _mediaConversionServiceMock.Verify(service => service.ExecuteConversion(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoEncodingSettings>(),
            It.IsAny<AudioTrackMapping[]>(),
            It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ConvertVideoFile_Mp4SourceWithVobSubSubtitle_FallsBackToFfmpegTargetingIdx()
    {
        _executableServiceMock
            .Setup(service => service.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, string.Empty, 0));

        var root = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mp4Path = Path.Combine(root, "clip.mp4");
        File.WriteAllText(mp4Path, "x");

        var mapping = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 160, 2)
        };

        _audioTrackMappingServiceMock.Setup(service => service.CreateDirectoryEncodeMappings(It.IsAny<MediaFile>()))
            .Returns(mapping);
        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mp4Path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFileWithEnglishVobSub(mp4Path));

        var mp4Output = Path.Combine(output, "clip.mp4");
        var idxOutput = Path.Combine(output, "clip.eng.sdh.idx");
        var subOutput = Path.Combine(output, "clip.eng.sdh.sub");
        SetupOutputPathResolution(mp4Output);
        SetupOutputPathResolution(subOutput);
        SetupOutputPathResolution(idxOutput);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", root)
            .AddParameter("OutputDirectory", output)
            .AddParameter("Ocr", SubtitleOcrMode.Skip);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _executableServiceMock.Verify(
            service => service.ExecuteAsync(
                "ffmpeg",
                It.Is<IEnumerable<string>>(args => args.Contains(idxOutput)),
                It.IsAny<CancellationToken>()),
            Times.Once);
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

    private static MediaFile CreateMediaFileWithEnglishSubrip(string path)
    {
        var video = new MediaStream(
            "video",
            0,
            "h264",
            string.Empty,
            string.Empty,
            new Dictionary<string, string>(),
            TimeSpan.Zero,
            null,
            @"{""index"":0,""codec_type"":""video""}");

        var audio = new MediaStream(
            "audio",
            1,
            "aac",
            string.Empty,
            string.Empty,
            new Dictionary<string, string> { ["language"] = "eng" },
            TimeSpan.Zero,
            "eng",
            @"{""index"":1,""codec_type"":""audio""}");

        var subtitle = new MediaStream(
            "subtitle",
            2,
            "subrip",
            string.Empty,
            string.Empty,
            new Dictionary<string, string> { ["language"] = "eng" },
            TimeSpan.Zero,
            "eng",
            @"{""index"":2,""codec_type"":""subtitle""}");

        return new MediaFile(
            path,
            new MediaFormat(path, 3, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[] { video, audio, subtitle },
            "{}");
    }

    private static MediaFile CreateMediaFileWithEnglishVobSub(string path)
    {
        var video = new MediaStream(
            "video",
            0,
            "h264",
            string.Empty,
            string.Empty,
            new Dictionary<string, string>(),
            TimeSpan.Zero,
            null,
            @"{""index"":0,""codec_type"":""video""}");

        var audio = new MediaStream(
            "audio",
            1,
            "aac",
            string.Empty,
            string.Empty,
            new Dictionary<string, string> { ["language"] = "eng" },
            TimeSpan.Zero,
            "eng",
            @"{""index"":1,""codec_type"":""audio""}");

        var subtitle = new MediaStream(
            "subtitle",
            2,
            "dvd_subtitle",
            string.Empty,
            string.Empty,
            new Dictionary<string, string> { ["language"] = "eng" },
            TimeSpan.Zero,
            "eng",
            @"{""index"":2,""codec_type"":""subtitle""}");

        return new MediaFile(
            path,
            new MediaFormat(path, 3, "mov,mp4,m4a,3gp,3g2,mj2", "QuickTime", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[] { video, audio, subtitle },
            "{}");
    }

    private static PowerShell CreatePowerShell(string commandName = "Convert-VideoFile") =>
        PowerShellCmdletTestHost.Create<ConvertVideoFileCommand>(commandName);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"MediaForgePS-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
