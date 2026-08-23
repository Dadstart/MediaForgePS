using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Services.TvDb;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.SeriesProcessing;

public class SeriesProcessingServiceCmdletIOTests
{
    [Fact]
    public void InvokeSeasonScan_WithoutUrls_WritesErrorViaICmdletIO()
    {
        var io = new FakeCmdletIO();
        var service = CreateService();

        var episodes = service.InvokeSeasonScan(io, season: 1, tvDbSeriesUrl: null, tvDbSeasonUrl: null, TestContext.Current.CancellationToken);

        Assert.Empty(episodes);
        var error = Assert.Single(io.Errors);
        Assert.Equal(ErrorCategory.InvalidArgument, error.CategoryInfo.Category);
        Assert.Contains("TvDbUrlMissing", error.FullyQualifiedErrorId, StringComparison.Ordinal);
    }

    [Fact]
    public void InvokeSeasonScan_WhenApiSucceeds_ReturnsEpisodes()
    {
        var io = new FakeCmdletIO();
        var tvDb = new Mock<ITvDbClient>();
        tvDb.Setup(client => client.ResolveSeriesIdAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(12345);
        tvDb.Setup(client => client.GetSeasonEpisodesAsync(12345, 1, "official", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TvDbEpisodeInfo("999", 1, "Pilot", 1)]);

        var service = CreateService(tvDb.Object);
        var episodes = service.InvokeSeasonScan(
            io,
            season: 1,
            tvDbSeriesUrl: "https://thetvdb.com/series/12345",
            tvDbSeasonUrl: null,
            TestContext.Current.CancellationToken);

        var episode = Assert.Single(episodes);
        Assert.Equal("999", episode.Id);
        Assert.Equal("Pilot", episode.Title);
        Assert.Empty(io.Errors);
    }

    [Fact]
    public void InvokeSeasonScan_WhenApiKeyMissing_WritesErrorViaICmdletIO()
    {
        var io = new FakeCmdletIO();
        var tvDb = new Mock<ITvDbClient>();
        tvDb.Setup(client => client.ResolveSeriesIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TvDbApiException(
                "TvDbApiKeyMissing",
                "TVDb API key is not configured. Set the TVDB_API_KEY environment variable."));

        var service = CreateService(tvDb.Object);
        var episodes = service.InvokeSeasonScan(
            io,
            season: 1,
            tvDbSeriesUrl: "https://thetvdb.com/series/my-show",
            tvDbSeasonUrl: null,
            TestContext.Current.CancellationToken);

        Assert.Empty(episodes);
        var error = Assert.Single(io.Errors);
        Assert.Equal(ErrorCategory.InvalidOperation, error.CategoryInfo.Category);
        Assert.Contains("TvDbApiKeyMissing", error.FullyQualifiedErrorId, StringComparison.Ordinal);
    }

    [Fact]
    public void NewProcessingDirectoryStructure_CreatesDirectoriesUsingPathContext()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS-CmdletIO-" + Guid.NewGuid().ToString("N"));
        try
        {
            var io = new FakeCmdletIO();
            io.Paths.CurrentLocationPath = root;
            var service = CreateService();

            var structure = service.NewProcessingDirectoryStructure(io, "Test Show", 2, subDirectories: ["Bonus"]);

            Assert.True(Directory.Exists(structure.RootDir));
            Assert.True(Directory.Exists(structure.SeasonDir));
            Assert.Contains("Season 02", structure.SeasonDir, StringComparison.Ordinal);
            Assert.Single(structure.SubDirs);
            Assert.True(Directory.Exists(structure.SubDirs[0]));
            Assert.Empty(io.Errors);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvokeSeasonScan_WhenCancelled_ThrowsOperationCanceledException()
    {
        var io = new FakeCmdletIO();
        var tvDb = new Mock<ITvDbClient>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        tvDb.Setup(client => client.ResolveSeriesIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = CreateService(tvDb.Object);

        Assert.Throws<OperationCanceledException>(() =>
            service.InvokeSeasonScan(
                io,
                season: 1,
                tvDbSeriesUrl: "https://thetvdb.com/series/my-show",
                tvDbSeasonUrl: null,
                cts.Token));
    }

    [Fact]
    public void InvokeSeasonScan_PassesCancellationTokenToTvDbClient()
    {
        var io = new FakeCmdletIO();
        var tvDb = new Mock<ITvDbClient>();
        using var cts = new CancellationTokenSource();
        tvDb.Setup(client => client.ResolveSeriesIdAsync("12345", cts.Token))
            .ReturnsAsync(12345);
        tvDb.Setup(client => client.GetSeasonEpisodesAsync(12345, 1, "official", cts.Token))
            .ReturnsAsync([new TvDbEpisodeInfo("1", 1, "Pilot", 1)]);

        var service = CreateService(tvDb.Object);
        var episodes = service.InvokeSeasonScan(
            io,
            season: 1,
            tvDbSeriesUrl: "https://thetvdb.com/series/12345",
            tvDbSeasonUrl: null,
            cts.Token);

        Assert.Single(episodes);
        tvDb.Verify(client => client.ResolveSeriesIdAsync("12345", cts.Token), Times.Once);
        tvDb.Verify(client => client.GetSeasonEpisodesAsync(12345, 1, "official", cts.Token), Times.Once);
    }

    [Fact]
    public void InvokeChapterExtractionPhase_WhenFfmpegFails_WritesErrorAndIncrementsFailed()
    {
        var io = new FakeCmdletIO();
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS-ChapterFail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "episode.mkv");
            File.WriteAllText(input, "video");
            var mediaReader = new Mock<IMediaReaderService>();
            mediaReader
                .Setup(reader => reader.GetMediaFileAsync(input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaFile(
                    input,
                    new MediaFormat(input, 1, "matroska", "Matroska", 10, 100, 1000, 1000, new Dictionary<string, string>()),
                    [new MediaChapter(0, 0, 10, new Dictionary<string, string>())],
                    Array.Empty<MediaStream>(),
                    "{}"));

            var executable = new Mock<IExecutableService>();
            executable
                .Setup(service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync(new ExecutableResult(null, "boom", 1));

            var service = new SeriesProcessingService(
                NullLogger<SeriesProcessingService>.Instance,
                mediaReader.Object,
                executable.Object,
                Mock.Of<ITvDbClient>());

            var stats = service.InvokeChapterExtractionPhase(
                io,
                root,
                [input],
                chapterNumber: 1,
                chapterDurationSeconds: 5,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(0, stats.Processed);
            Assert.Equal(1, stats.Failed);
            Assert.Contains(io.Errors, error => error.FullyQualifiedErrorId.Contains("ChapterExtractionFailed", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static SeriesProcessingService CreateService(ITvDbClient? tvDbClient = null) =>
        new(
            NullLogger<SeriesProcessingService>.Instance,
            Mock.Of<IMediaReaderService>(),
            Mock.Of<IExecutableService>(),
            tvDbClient ?? Mock.Of<ITvDbClient>());
}
