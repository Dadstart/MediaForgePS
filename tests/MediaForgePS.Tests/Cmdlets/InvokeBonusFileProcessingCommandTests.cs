using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class InvokeBonusFileProcessingCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock = new();
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock = new();
    private readonly Mock<IMediaConversionService> _mediaConversionServiceMock = new();
    private readonly Mock<IExecutableService> _executableServiceMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<InvokeBonusFileProcessingCommand>> _loggerMock = new();
    private readonly Mock<IDebuggerService> _debuggerServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;
    private readonly List<string> _tempDirectories = new();

    public InvokeBonusFileProcessingCommandTests()
    {
        _loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(debugger => debugger.BreakIfDebugging(It.IsAny<bool>()));

        var ocrConverterMock = new Mock<IImageSubtitleOcrConverter>();
        ocrConverterMock.SetupGet(c => c.IsSupportedOnCurrentPlatform).Returns(true);
        ocrConverterMock.SetupGet(c => c.IsAvailable).Returns(true);
        ocrConverterMock.SetupGet(c => c.ExpectedTessDataDescription).Returns("tessdata expected");

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_mediaReaderServiceMock.Object);
        services.AddSingleton(_mediaConversionServiceMock.Object);
        services.AddSingleton(_executableServiceMock.Object);
        services.AddSingleton<IImageSubtitleOcrConverter>(ocrConverterMock.Object);
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton<IBonusProcessingService>(sp => new BonusProcessingService(
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<BonusProcessingService>(),
            sp.GetRequiredService<IMediaReaderService>(),
            sp.GetRequiredService<IMediaConversionService>(),
            sp.GetRequiredService<IExecutableService>(),
            sp.GetRequiredService<IPathResolver>()));

        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();

        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void Defaults_AreInitializedCorrectly()
    {
        var cmdlet = new InvokeBonusFileProcessingCommand();

        Assert.NotNull(cmdlet);
        Assert.Equal("nvenc", cmdlet.DefaultVideoEncoder);
        Assert.Equal(string.Empty, cmdlet.InputPath);
        Assert.Equal(string.Empty, cmdlet.OutputPath);
        Assert.Equal(SubtitleOcrMode.Auto, cmdlet.Ocr);
    }

    [Fact]
    public void InvokeBonusFileProcessing_UsesOcrParameter()
    {
        Assert.NotNull(typeof(InvokeBonusFileProcessingCommand).GetProperty(nameof(InvokeBonusFileProcessingCommand.Ocr)));
        Assert.Null(typeof(InvokeBonusFileProcessingCommand).GetProperty("SkipOcr"));
    }

    [Fact]
    public void CreateDefaultVideoEncodingSettings_UsesNvencSettings_WhenEncoderIsNvenc()
    {
        var cmdlet = new InvokeBonusFileProcessingCommand
        {
            DefaultVideoEncoder = "nvenc"
        };

        var settings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(cmdlet.DefaultVideoEncoder);

        var nvencSettings = Assert.IsType<NvencVideoEncodingSettings>(settings);
        Assert.Equal("hevc_nvenc", nvencSettings.Codec);
    }

    [Fact]
    public void CreateDefaultVideoEncodingSettings_UsesLibx264_WhenEncoderIsX264()
    {
        var cmdlet = new InvokeBonusFileProcessingCommand
        {
            DefaultVideoEncoder = "x264"
        };

        var settings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(cmdlet.DefaultVideoEncoder);

        var crfSettings = Assert.IsType<ConstantRateVideoEncodingSettings>(settings);
        Assert.Equal("libx264", crfSettings.Codec);
    }

    [Fact]
    public void CreateDefaultVideoEncodingSettings_DefaultsToLibx265_WhenEncoderNotSpecified()
    {
        var cmdlet = new InvokeBonusFileProcessingCommand
        {
            DefaultVideoEncoder = null!
        };

        var settings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(cmdlet.DefaultVideoEncoder);

        var crfSettings = Assert.IsType<ConstantRateVideoEncodingSettings>(settings);
        Assert.Equal("libx265", crfSettings.Codec);
    }

    [Fact]
    public void CreateAudioTrackMappings_CreatesCopyMapping_ForMultiChannelDts()
    {
        var streams = new List<MediaStream>
        {
            CreateAudioStream(1, "dts", "eng", 6, "DTS 5.1")
        };

        var mappings = MediaConversionHelper.CreateAutomaticAudioTrackMappings(streams);

        Assert.Single(mappings);
        var copy = Assert.IsType<CopyAudioTrackMapping>(mappings[0]);
        Assert.Equal(0, copy.SourceIndex);
        Assert.Equal(0, copy.DestinationIndex);
    }

    [Fact]
    public void CreateAudioTrackMappings_CreatesEncodeMapping_ForAac()
    {
        var streams = new List<MediaStream>
        {
            CreateAudioStream(1, "aac", "eng", 2, "AAC 2.0")
        };

        var mappings = MediaConversionHelper.CreateAutomaticAudioTrackMappings(streams);

        Assert.Single(mappings);
        var encode = Assert.IsType<EncodeAudioTrackMapping>(mappings[0]);
        Assert.Equal(0, encode.SourceIndex);
        Assert.Equal(0, encode.DestinationIndex);
        Assert.Equal("aac", encode.DestinationCodec);
        Assert.Equal(2, encode.DestinationChannels);
    }

    [Fact]
    public void CreateAudioTrackMappings_SwapsDtsAndMultiChannelAacOrder()
    {
        var streams = new List<MediaStream>
        {
            CreateAudioStream(1, "dts", "eng", 6, "DTS 5.1"),
            CreateAudioStream(2, "aac", "eng", 6, "AAC 5.1")
        };

        var mappings = MediaConversionHelper.CreateAutomaticAudioTrackMappings(streams);

        Assert.Equal(2, mappings.Length);

        var first = mappings[0];
        var second = mappings[1];

        var firstEncode = Assert.IsType<EncodeAudioTrackMapping>(first);
        var secondCopy = Assert.IsType<CopyAudioTrackMapping>(second);

        Assert.Equal(0, firstEncode.DestinationIndex);
        Assert.Equal(1, secondCopy.DestinationIndex);
        Assert.Equal(6, firstEncode.DestinationChannels);
        Assert.Equal("aac", firstEncode.DestinationCodec);
    }

    [Fact]
    public void InvokeBonusFileProcessing_SupportsShouldProcess()
    {
        var attribute = typeof(InvokeBonusFileProcessingCommand)
            .GetCustomAttributes(typeof(CmdletAttribute), inherit: false)
            .Cast<CmdletAttribute>()
            .Single();

        Assert.True(attribute.SupportsShouldProcess);
    }

    [Fact]
    public void InvokeBonusFileProcessing_WithWhatIf_DoesNotConvertFiles()
    {
        var input = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(input, "clip-trailer.mkv");
        File.WriteAllText(mkvPath, "x");

        SetupOutputPathResolution(output);

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-BonusFileProcessing")
            .AddParameter("InputPath", input)
            .AddParameter("OutputPath", output)
            .AddParameter("SkipSubtitles")
            .AddParameter("WhatIf");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(
            service => service.ExecuteConversion(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()),
            Times.Never);
        _mediaReaderServiceMock.Verify(
            service => service.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void InvokeBonusFileProcessing_WritesConversionResultWithSizeReductionAndDuration()
    {
        var input = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(input, "clip-trailer.mkv");
        File.WriteAllBytes(mkvPath, new byte[1000]);

        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));
        SetupOutputPathResolution(output);

        var expectedOutput = Path.Combine(input, "clip-trailer.mp4");
        _mediaConversionServiceMock
            .Setup(service => service.ExecuteConversion(
                mkvPath,
                expectedOutput,
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback(() => File.WriteAllBytes(expectedOutput, new byte[400]));

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-BonusFileProcessing")
            .AddParameter("InputPath", input)
            .AddParameter("OutputPath", output)
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

        var statistics = Assert.Single(results.Select(r => r.BaseObject).OfType<MediaConversionStatistics>());
        Assert.Equal(1, statistics.FileCount);
        Assert.Equal(60.0, statistics.AverageSizeReductionPercent);
        Assert.Equal(Math.Round(MediaConversionHelper.BytesToMegabytes(1000), 1), statistics.AverageInputSizeMegabytes);
        Assert.Equal(Math.Round(MediaConversionHelper.BytesToMegabytes(400), 1), statistics.AverageOutputSizeMegabytes);
    }

    [Fact]
    public void InvokeBonusFileProcessing_WithEncodeProgress_WritesPercentAndSecondsRemaining()
    {
        var input = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(input, "clip-trailer.mkv");
        File.WriteAllText(mkvPath, "x");

        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));
        SetupOutputPathResolution(output);

        _mediaConversionServiceMock
            .Setup(service => service.ExecuteConversion(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback((
                string _,
                string _,
                VideoEncodingSettings _,
                AudioTrackMapping[] _,
                string[]? _,
                IProgress<FfmpegProgress>? progress, CancellationToken _, bool _, TimeSpan? _) =>
            {
                progress?.Report(new FfmpegProgress(
                    TimeSpan.FromSeconds(25),
                    TimeSpan.FromSeconds(100),
                    25,
                    TimeSpan.FromSeconds(12.1)));
                Thread.Sleep(200);
            });

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-BonusFileProcessing")
            .AddParameter("InputPath", input)
            .AddParameter("OutputPath", output)
            .AddParameter("SkipSubtitles")
            .AddParameter("Confirm", false);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();
        var progressRecords = ps.Streams.Progress.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(
            progressRecords,
            record => record.Activity == "Current file"
                && record.StatusDescription.Contains("00:25 / 01:40", StringComparison.Ordinal)
                && record.PercentComplete == 25
                && record.SecondsRemaining == 13);
        Assert.Contains(
            progressRecords,
            record => record.Activity == "Bonus file conversion");
    }

    [Fact]
    public void InvokeBonusFileProcessing_WithOneSecondRemaining_WritesFinishingSpinner()
    {
        var input = CreateTempDirectory();
        var output = CreateTempDirectory();
        var mkvPath = Path.Combine(input, "clip-featurette.mkv");
        File.WriteAllText(mkvPath, "x");

        _mediaReaderServiceMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));
        SetupOutputPathResolution(output);

        _mediaConversionServiceMock
            .Setup(service => service.ExecuteConversion(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback((
                string _,
                string _,
                VideoEncodingSettings _,
                AudioTrackMapping[] _,
                string[]? _,
                IProgress<FfmpegProgress>? progress, CancellationToken _, bool _, TimeSpan? _) =>
            {
                progress?.Report(new FfmpegProgress(
                    TimeSpan.FromSeconds(99),
                    TimeSpan.FromSeconds(100),
                    99,
                    TimeSpan.FromSeconds(1)));
                Thread.Sleep(200);
            });

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-BonusFileProcessing")
            .AddParameter("InputPath", input)
            .AddParameter("OutputPath", output)
            .AddParameter("SkipSubtitles")
            .AddParameter("Confirm", false);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();
        var progressRecords = ps.Streams.Progress.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(
            progressRecords,
            record => record.Activity == "Current file"
                && record.StatusDescription.StartsWith("finishing ", StringComparison.Ordinal)
                && record.PercentComplete == 99
                && record.SecondsRemaining == -1);
    }

    private void SetupOutputPathResolution(string outputPath)
    {
        _pathResolverMock
            .Setup(resolver => resolver.TryResolveOutputPath(outputPath, out It.Ref<string>.IsAny))
            .Returns((string path, out string resolved) =>
            {
                resolved = path;
                return true;
            });
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"MediaForgePS-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private static PowerShell CreatePowerShell() =>
        PowerShellCmdletTestHost.Create<InvokeBonusFileProcessingCommand>("Invoke-BonusFileProcessing");

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
            2);

        return new MediaFile(
            path,
            new MediaFormat(path, 2, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                new MediaStream("video", 0, "h264", string.Empty, string.Empty, new Dictionary<string, string>(), TimeSpan.Zero, null, Channels: 0),
                stream
            });
    }

    private static MediaStream CreateAudioStream(int index, string codec, string language, int channels, string? title = null)
    {
        var tags = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(language))
            tags["language"] = language;
        if (!string.IsNullOrEmpty(title))
            tags["title"] = title;

        return new MediaStream(
            "audio",
            index,
            codec,
            string.Empty,
            string.Empty,
            tags,
            TimeSpan.Zero,
            language,
            channels);
    }
}
