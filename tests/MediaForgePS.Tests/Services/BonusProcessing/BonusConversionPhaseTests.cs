using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.BonusProcessing;

public sealed class BonusConversionPhaseTests : IDisposable
{
    private readonly string _root;

    public BonusConversionPhaseTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "MediaForgePS_BonusConversion_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Run_WhenNoBonusMkvFiles_ReturnsEmptyResult()
    {
        var phase = CreatePhase(out _, out _);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: false), emitResult: null, TestContext.Current.CancellationToken);

        Assert.Empty(result.Results);
        Assert.Equal(0, result.DiscoveredFileCount);
        Assert.Contains(io.VerboseMessages, message => message.Contains("No bonus-suffix MKV files", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenMediaReadReturnsNull_ReturnsFailedResult()
    {
        var mkvPath = CreateBonusMkv("clip-trailer.mkv");
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFile?)null);
        var phase = CreatePhase(mediaReaderMock, out _);
        var io = new FakeCmdletIO();
        var emitted = new List<MediaConversionResult>();

        var result = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: false), emitted.Add, TestContext.Current.CancellationToken);

        var summary = Assert.Single(result.Results);
        Assert.False(MediaConversionHelper.IsCompletedConversion(summary));
        Assert.Contains("Failed to read media file information", summary.Status, StringComparison.Ordinal);
        Assert.Contains(io.Warnings, warning => warning.Contains("Failed to read media file information", StringComparison.Ordinal));
        Assert.Equal(summary, Assert.Single(emitted));
    }

    [Fact]
    public void Run_WhenMediaReadThrows_ReturnsFailedResult()
    {
        var mkvPath = CreateBonusMkv("clip-trailer.mkv");
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("ffprobe failed"));
        var phase = CreatePhase(mediaReaderMock, out _);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: false), emitResult: null, TestContext.Current.CancellationToken);

        var summary = Assert.Single(result.Results);
        Assert.False(MediaConversionHelper.IsCompletedConversion(summary));
        Assert.Contains("Failed to read media file", summary.Status, StringComparison.Ordinal);
        Assert.Contains(io.Warnings, warning => warning.Contains("Failed to read media file", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenVideoOnly_ConvertsWithoutAudioMappings()
    {
        var mkvPath = CreateBonusMkv("clip-trailer.mkv");
        var media = CreateVideoOnlyMediaFile(mkvPath);
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);
        var phase = CreatePhase(mediaReaderMock, out var conversionMock);
        var io = new FakeCmdletIO();
        var outputPath = Path.Combine(_root, "clip-trailer.mp4");
        SetupSuccessfulConversion(conversionMock, mkvPath, outputPath);

        var result = phase.Run(io, new BonusConversionRequest(_root, "x264", Force: false), emitResult: null, TestContext.Current.CancellationToken);

        var summary = Assert.Single(result.Results);
        Assert.True(MediaConversionHelper.IsCompletedConversion(summary));
        conversionMock.Verify(
            service => service.ExecuteConversion(
                mkvPath,
                outputPath,
                It.IsAny<VideoEncodingSettings>(),
                It.Is<AudioTrackMapping[]>(mappings => mappings.Length == 0),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public void Run_WhenOutputExistsWithoutForce_SkipsConversionAndWritesError()
    {
        var mkvPath = CreateBonusMkv("clip-trailer.mkv");
        var outputPath = Path.Combine(_root, "clip-trailer.mp4");
        File.WriteAllBytes(outputPath, [1, 2, 3]);
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));
        var phase = CreatePhase(mediaReaderMock, out var conversionMock);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: false), emitResult: null, TestContext.Current.CancellationToken);

        var summary = Assert.Single(result.Results);
        Assert.False(MediaConversionHelper.IsCompletedConversion(summary));
        Assert.Contains("Output file already exists", summary.Status, StringComparison.Ordinal);
        Assert.Contains(io.Errors, error => error.FullyQualifiedErrorId.Contains("OutputFileExists", StringComparison.Ordinal));
        conversionMock.Verify(
            service => service.ExecuteConversion(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact]
    public void Run_WhenOutputExistsWithForce_OverwritesExistingOutput()
    {
        var mkvPath = CreateBonusMkv("clip-trailer.mkv");
        var outputPath = Path.Combine(_root, "clip-trailer.mp4");
        File.WriteAllBytes(outputPath, [1, 2, 3]);
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));
        var phase = CreatePhase(mediaReaderMock, out var conversionMock);
        SetupSuccessfulConversion(conversionMock, mkvPath, outputPath);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: true), emitResult: null, TestContext.Current.CancellationToken);

        var summary = Assert.Single(result.Results);
        Assert.True(MediaConversionHelper.IsCompletedConversion(summary));
        conversionMock.Verify(
            service => service.ExecuteConversion(
                mkvPath,
                outputPath,
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(),
                true,
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public void Run_WhenFfmpegConversionFails_ReturnsFailedResult()
    {
        var mkvPath = CreateBonusMkv("clip-featurette.mkv");
        var outputPath = Path.Combine(_root, "clip-featurette.mp4");
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));
        var phase = CreatePhase(mediaReaderMock, out var conversionMock);
        conversionMock
            .Setup(service => service.ExecuteConversion(
                mkvPath,
                outputPath,
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan?>()))
            .Throws(new FfmpegConversionException("conversion failed", mkvPath, outputPath, 1, "Invalid data found"));
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: false), emitResult: null, TestContext.Current.CancellationToken);

        var summary = Assert.Single(result.Results);
        Assert.False(MediaConversionHelper.IsCompletedConversion(summary));
        Assert.Contains("Conversion failed", summary.Status, StringComparison.Ordinal);
        Assert.Contains("Invalid data found", summary.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WhenConversionThrowsGenericException_ReturnsFailedResult()
    {
        var mkvPath = CreateBonusMkv("clip-featurette.mkv");
        var outputPath = Path.Combine(_root, "clip-featurette.mp4");
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mkvPath));
        var phase = CreatePhase(mediaReaderMock, out var conversionMock);
        conversionMock
            .Setup(service => service.ExecuteConversion(
                mkvPath,
                outputPath,
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan?>()))
            .Throws(new InvalidOperationException("disk full"));
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: false), emitResult: null, TestContext.Current.CancellationToken);

        var summary = Assert.Single(result.Results);
        Assert.False(MediaConversionHelper.IsCompletedConversion(summary));
        Assert.Contains("Conversion failed: disk full", summary.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WhenMultipleBonusFiles_ConvertsEachAndEmitsResults()
    {
        var firstMkv = CreateBonusMkv("clip-trailer.mkv", 800);
        var secondMkv = CreateBonusMkv("clip-featurette.mkv", 1200);
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(firstMkv, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(firstMkv));
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(secondMkv, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(secondMkv));
        var phase = CreatePhase(mediaReaderMock, out var conversionMock);
        var firstOutput = Path.Combine(_root, "clip-trailer.mp4");
        var secondOutput = Path.Combine(_root, "clip-featurette.mp4");
        SetupSuccessfulConversion(conversionMock, firstMkv, firstOutput);
        SetupSuccessfulConversion(conversionMock, secondMkv, secondOutput);
        var io = new FakeCmdletIO();
        var emitted = new List<MediaConversionResult>();

        var result = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: false), emitted.Add, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.DiscoveredFileCount);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal(2, emitted.Count);
        Assert.All(result.Results, summary => Assert.True(MediaConversionHelper.IsCompletedConversion(summary)));
        Assert.Contains(io.ProgressRecords, record => record.Activity == "Bonus file conversion");
        Assert.Contains(io.VerboseMessages, message => message.Contains("Converting 2 bonus file(s)", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenSecondFileHasBatchHistory_ReportsBatchProgressDuringEncode()
    {
        var firstMkv = CreateBonusMkv("clip-trailer.mkv", 800);
        var secondMkv = CreateBonusMkv("clip-featurette.mkv", 1200);
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(firstMkv, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(firstMkv));
        mediaReaderMock.Setup(service => service.GetMediaFileAsync(secondMkv, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(secondMkv));
        var phase = CreatePhase(mediaReaderMock, out var conversionMock);
        var firstOutput = Path.Combine(_root, "clip-trailer.mp4");
        var secondOutput = Path.Combine(_root, "clip-featurette.mp4");
        SetupSuccessfulConversion(conversionMock, firstMkv, firstOutput, reportProgress: false);
        SetupSuccessfulConversion(
            conversionMock,
            secondMkv,
            secondOutput,
            reportProgress: true,
            progress: new FfmpegProgress(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(40), 25, TimeSpan.FromSeconds(30)));
        var io = new FakeCmdletIO();

        _ = phase.Run(io, new BonusConversionRequest(_root, "nvenc", Force: false), emitResult: null, TestContext.Current.CancellationToken);

        Assert.Contains(
            io.ProgressRecords,
            record => record.Activity == "Bonus file conversion" && record.PercentComplete > 0);
        Assert.Contains(
            io.ProgressRecords,
            record => record.Activity == "Current file" && record.StatusDescription.Contains("Encoding to", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenCancelled_ThrowsOperationCanceledException()
    {
        CreateBonusMkv("clip-trailer.mkv");
        var phase = CreatePhase(out _, out _);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => phase.Run(new FakeCmdletIO(), new BonusConversionRequest(_root, "nvenc", Force: false), emitResult: null, cts.Token));
    }

    private string CreateBonusMkv(string fileName, int sizeBytes = 1000)
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllBytes(path, new byte[sizeBytes]);
        return path;
    }

    private static BonusConversionPhase CreatePhase(
        out Mock<IMediaReaderService> mediaReaderMock,
        out Mock<IMediaConversionService> conversionMock) =>
        CreatePhase(mediaReaderMock = new Mock<IMediaReaderService>(), out conversionMock);

    private static BonusConversionPhase CreatePhase(
        Mock<IMediaReaderService> mediaReaderMock,
        out Mock<IMediaConversionService> conversionMock)
    {
        conversionMock = new Mock<IMediaConversionService>();
        return new BonusConversionPhase(mediaReaderMock.Object, conversionMock.Object, NullLogger.Instance);
    }

    private static void SetupSuccessfulConversion(
        Mock<IMediaConversionService> conversionMock,
        string inputPath,
        string outputPath,
        bool reportProgress = false,
        FfmpegProgress? progress = null)
    {
        conversionMock
            .Setup(service => service.ExecuteConversion(
                inputPath,
                outputPath,
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan?>()))
            .Callback((
                string _,
                string _,
                VideoEncodingSettings _,
                AudioTrackMapping[] _,
                string[]? _,
                IProgress<FfmpegProgress>? progressReporter,
                CancellationToken _,
                bool _,
                TimeSpan? _) =>
            {
                if (reportProgress)
                    progressReporter?.Report(progress ?? new FfmpegProgress(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20), 25, TimeSpan.FromSeconds(15)));

                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(outputPath, new byte[400]);
            });
    }

    private static MediaFile CreateMediaFile(string path) =>
        new(
            path,
            new MediaFormat(path, 2, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            [],
            [
                new MediaStream("video", 0, "h264", string.Empty, string.Empty, new Dictionary<string, string>(), TimeSpan.Zero, null, Channels: 0),
                new MediaStream("audio", 1, "aac", string.Empty, string.Empty, new Dictionary<string, string> { ["language"] = "eng" }, TimeSpan.Zero, "eng", 2)
            ]);

    private static MediaFile CreateVideoOnlyMediaFile(string path) =>
        new(
            path,
            new MediaFormat(path, 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            [],
            [
                new MediaStream("video", 0, "h264", string.Empty, string.Empty, new Dictionary<string, string>(), TimeSpan.Zero, null, Channels: 0)
            ]);
}
