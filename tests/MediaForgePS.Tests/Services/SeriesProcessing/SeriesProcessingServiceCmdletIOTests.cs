using System;
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

        var episodes = service.InvokeSeasonScan(io, season: 1, tvDbSeriesUrl: null, tvDbSeasonUrl: null);

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
            tvDbSeasonUrl: null);

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
            tvDbSeasonUrl: null);

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

    private static SeriesProcessingService CreateService(ITvDbClient? tvDbClient = null) =>
        new(
            NullLogger<SeriesProcessingService>.Instance,
            Mock.Of<IMediaReaderService>(),
            Mock.Of<IExecutableService>(),
            tvDbClient ?? Mock.Of<ITvDbClient>());
}
