using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.SeriesProcessing;

public sealed class SeriesCaptionExtractionPhaseTests : IDisposable
{
    private readonly string _root;

    public SeriesCaptionExtractionPhaseTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "MediaForgePS_SeriesCaptionExtraction_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Run_WhenCopiedFilesEmpty_ReturnsZeroCounts()
    {
        var phase = CreatePhase(out _, out _);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, _root, [], "Captions", CreateDirectory, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Processed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.Total);
        Assert.Empty(result.ExtractedCaptionPaths);
        Assert.Contains(io.ProgressRecords, record => record.Activity.Contains("Caption extraction", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenMediaReadReturnsNull_CountsFileAsFailed()
    {
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFile?)null);
        var phase = CreatePhase(mediaReaderMock, out _);
        var io = new FakeCmdletIO();
        var copiedFile = CreateCopiedFile("Episode 1.mkv");

        var result = phase.Run(io, _root, [copiedFile], "Captions", CreateDirectory, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Processed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.Total);
        Assert.Empty(result.ExtractedCaptionPaths);
    }

    [Fact]
    public void Run_WhenMediaHasEnglishSubtitle_ExtractsIntoCaptionDirectory()
    {
        var copiedFile = CreateCopiedFile("Episode 1.mkv");
        var media = CreateMediaFile(copiedFile, CreateStream("subrip"));
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(copiedFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);
        var phase = CreatePhase(mediaReaderMock, out var executableMock);
        var io = new FakeCmdletIO();
        string? captionDir = null;

        var result = phase.Run(
            io,
            _root,
            [copiedFile],
            "Captions",
            (path, _) =>
            {
                Directory.CreateDirectory(path);
                captionDir = path;
                return path;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, result.Total);
        var extractedPath = Assert.Single(result.ExtractedCaptionPaths);
        Assert.NotNull(captionDir);
        Assert.StartsWith(captionDir, extractedPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Episode 1.eng.sdh.srt", Path.GetFileName(extractedPath));
        Assert.True(File.Exists(extractedPath));
        executableMock.Verify(
            e => e.ExecuteAsync(
                "ffmpeg",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()),
            Times.AtLeastOnce);
        Assert.Contains(io.ProgressRecords, record => record.Activity.Contains("Caption extraction", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenMediaHasNoEnglishSubtitles_CountsFileAsFailed()
    {
        var copiedFile = CreateCopiedFile("Episode 1.mkv");
        var media = CreateMediaFile(copiedFile, CreateStream("subrip", language: "spa"));
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(copiedFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);
        var phase = CreatePhase(mediaReaderMock, out _);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, _root, [copiedFile], "Captions", CreateDirectory, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Processed);
        Assert.Equal(1, result.Failed);
        Assert.Empty(result.ExtractedCaptionPaths);
    }

    [Fact]
    public void Run_WhenMediaReadThrows_CountsFileAsFailed()
    {
        var copiedFile = CreateCopiedFile("Episode 1.mkv");
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(copiedFile, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("ffprobe failed"));
        var phase = CreatePhase(mediaReaderMock, out _);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, _root, [copiedFile], "Captions", CreateDirectory, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Processed);
        Assert.Equal(1, result.Failed);
        Assert.Empty(result.ExtractedCaptionPaths);
    }

    [Fact]
    public void Run_WhenCancelled_ThrowsOperationCanceledException()
    {
        var copiedFile = CreateCopiedFile("Episode 1.mkv");
        var phase = CreatePhase(out _, out _);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => phase.Run(new FakeCmdletIO(), _root, [copiedFile], "Captions", CreateDirectory, cts.Token));
    }

    [Fact]
    public void Run_CallsCreateDirectoryWithSeasonCaptionSubfolder()
    {
        var phase = CreatePhase(out _, out _);
        var io = new FakeCmdletIO();
        var copiedFile = CreateCopiedFile("Episode 1.mkv");
        string? createDirectoryPath = null;
        string? createDirectoryLabel = null;

        phase.Run(
            io,
            _root,
            [copiedFile],
            "Captions",
            (path, label) =>
            {
                createDirectoryPath = path;
                createDirectoryLabel = label;
                return CreateDirectory(path, label);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_root, "Captions"), createDirectoryPath);
        Assert.Equal("caption", createDirectoryLabel);
        Assert.True(Directory.Exists(createDirectoryPath));
    }

    [Fact]
    public void Run_WhenMultipleFiles_MixesProcessedAndFailedCounts()
    {
        var successFile = CreateCopiedFile("Episode 1.mkv");
        var missingMediaFile = CreateCopiedFile("Episode 2.mkv");
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(successFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(successFile, CreateStream("subrip")));
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(missingMediaFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFile?)null);
        var phase = CreatePhase(mediaReaderMock, out _);
        var io = new FakeCmdletIO();

        var result = phase.Run(
            io,
            _root,
            [successFile, missingMediaFile],
            "Captions",
            CreateDirectory,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(2, result.Total);
        Assert.Single(result.ExtractedCaptionPaths);
    }

    private string CreateCopiedFile(string fileName)
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllText(path, "video");
        return path;
    }

    private static string CreateDirectory(string path, string _)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static SeriesCaptionExtractionPhase CreatePhase(
        out Mock<IMediaReaderService> mediaReaderMock,
        out Mock<IExecutableService> executableMock) =>
        CreatePhase(mediaReaderMock = new Mock<IMediaReaderService>(), out executableMock);

    private static SeriesCaptionExtractionPhase CreatePhase(
        Mock<IMediaReaderService> mediaReaderMock,
        out Mock<IExecutableService> executableMock)
    {
        executableMock = CreateExecutableMock();
        return new SeriesCaptionExtractionPhase(
            mediaReaderMock.Object,
            executableMock.Object,
            NullLogger.Instance);
    }

    private static Mock<IExecutableService> CreateExecutableMock()
    {
        var mock = new Mock<IExecutableService>();
        mock.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .Callback<string, IEnumerable<string>, CancellationToken, TimeSpan?>((exe, args, _, __) =>
                MaterializeExtractOutputs(exe, args.ToArray()))
            .ReturnsAsync(new ExecutableResult(string.Empty, string.Empty, 0));
        return mock;
    }

    private static void MaterializeExtractOutputs(string exe, string[] args)
    {
        if (string.Equals(exe, "ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            WriteStagedFile(args[^1]);
            return;
        }

        var trackArg = args.FirstOrDefault(static argument =>
        {
            var separator = argument.IndexOf(':');
            return separator > 0
                && separator < argument.Length - 1
                && int.TryParse(argument.AsSpan(0, separator), out _);
        });
        if (trackArg is null)
            return;

        var outputPath = trackArg[(trackArg.IndexOf(':') + 1)..];
        WriteStagedFile(outputPath);
        if (outputPath.EndsWith(".sub", StringComparison.OrdinalIgnoreCase))
            WriteStagedFile(Path.ChangeExtension(outputPath, ".idx"));
    }

    private static void WriteStagedFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, "staged");
    }

    private static MediaStream CreateStream(string codec, int index = 2, string? language = "eng") =>
        new(
            Type: "subtitle",
            Index: index,
            Codec: codec,
            Profile: string.Empty,
            CodecLongName: string.Empty,
            Tags: new Dictionary<string, string>(),
            Language: language);

    private static MediaFile CreateMediaFile(string path, params MediaStream[] streams) =>
        new(
            path,
            new MediaFormat(path, streams.Length, "matroska", "Matroska", 0, 1, 0, 0, new Dictionary<string, string>()),
            [],
            streams);
}
