using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.BonusProcessing;

public sealed class BonusCaptionExtractionPhaseTests : IDisposable
{
    private readonly string _root;

    public BonusCaptionExtractionPhaseTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "MediaForgePS_BonusCaptionExtraction_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Run_WhenNoBonusMkvFiles_ReturnsEmptyList()
    {
        var phase = CreatePhase(out _, out _, out _);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusCaptionExtractionRequest(_root), TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Empty(io.ProgressRecords);
    }

    [Fact]
    public void Run_WhenMediaReadThrows_ContinuesAndReturnsEmpty()
    {
        var mkvPath = CreateBonusMkv("clip-trailer.mkv");
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("ffprobe failed"));
        var phase = CreatePhase(mediaReaderMock, out _, new Mock<IPathResolver>());
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusCaptionExtractionRequest(_root), TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Contains(io.ProgressRecords, record => record.Activity.Contains("Subtitle extraction", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenMediaHasEnglishSubtitle_ExtractsSubtitleFile()
    {
        var mkvPath = CreateBonusMkv("clip-trailer.mkv");
        var media = CreateMediaFile(mkvPath, CreateStream("subrip"));
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);
        var pathResolverMock = new Mock<IPathResolver>();
        pathResolverMock.Setup(r => r.TryResolveOutputPath(It.IsAny<string>(), out It.Ref<string?>.IsAny))
            .Returns((string path, out string resolved) =>
            {
                resolved = path;
                return true;
            });
        var phase = CreatePhase(mediaReaderMock, out var executableMock, pathResolverMock);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusCaptionExtractionRequest(_root), TestContext.Current.CancellationToken);

        var extractedPath = Assert.Single(result);
        Assert.Equal("clip-trailer.eng.sdh.srt", Path.GetFileName(extractedPath));
        Assert.True(File.Exists(extractedPath));
        Assert.Contains(io.VerboseMessages, message => message.StartsWith("Extracted ", StringComparison.Ordinal));
        executableMock.Verify(
            e => e.ExecuteAsync(
                "ffmpeg",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Run_WhenMediaHasNoEnglishSubtitles_WritesVerboseAndReturnsEmpty()
    {
        var mkvPath = CreateBonusMkv("clip-featurette.mkv");
        var media = CreateMediaFile(mkvPath, CreateStream("subrip", language: "spa"));
        var mediaReaderMock = new Mock<IMediaReaderService>();
        mediaReaderMock.Setup(m => m.GetMediaFileAsync(mkvPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);
        var pathResolverMock = new Mock<IPathResolver>();
        pathResolverMock.Setup(r => r.TryResolveOutputPath(It.IsAny<string>(), out It.Ref<string?>.IsAny))
            .Returns((string path, out string resolved) =>
            {
                resolved = path;
                return true;
            });
        var phase = CreatePhase(mediaReaderMock, out _, pathResolverMock);
        var io = new FakeCmdletIO();

        var result = phase.Run(io, new BonusCaptionExtractionRequest(_root), TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Contains(io.VerboseMessages, message => message.Contains("No English subtitles", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenCancelled_ThrowsOperationCanceledException()
    {
        CreateBonusMkv("clip-trailer.mkv");
        var phase = CreatePhase(out _, out _, out _);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => phase.Run(new FakeCmdletIO(), new BonusCaptionExtractionRequest(_root), cts.Token));
    }

    private string CreateBonusMkv(string fileName)
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllText(path, "video");
        return path;
    }

    private static BonusCaptionExtractionPhase CreatePhase(
        out Mock<IMediaReaderService> mediaReaderMock,
        out Mock<IExecutableService> executableMock,
        out Mock<IPathResolver> pathResolverMock) =>
        CreatePhase(
            mediaReaderMock = new Mock<IMediaReaderService>(),
            out executableMock,
            pathResolverMock = new Mock<IPathResolver>());

    private static BonusCaptionExtractionPhase CreatePhase(
        Mock<IMediaReaderService> mediaReaderMock,
        out Mock<IExecutableService> executableMock,
        Mock<IPathResolver> pathResolverMock)
    {
        executableMock = CreateExecutableMock();
        pathResolverMock.Setup(r => r.TryResolveOutputPath(It.IsAny<string>(), out It.Ref<string?>.IsAny))
            .Returns((string path, out string resolved) =>
            {
                resolved = path;
                return true;
            });
        return new BonusCaptionExtractionPhase(
            mediaReaderMock.Object,
            executableMock.Object,
            pathResolverMock.Object,
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
