using System;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.ComponentTests.Infrastructure;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.E2ETests.Cmdlets;

public class SeriesProcessingPipelineE2ETests : E2ETestBase
{
    private const string StubSeriesUrl = "https://thetvdb.com/series/component-test-show";

    [Fact(Timeout = 180_000)]
    public void PackedModule_StubbedSeasonScanAndVideoCopy_ProducesNamedEpisodeFile()
    {
        var provider = ComponentServiceProviderFactory.CreateWithStubTvDb(
            new StubTvDbClient(StubTvDbClient.DefaultSeasonOneEpisodes[0]));
        using var moduleScope = new ComponentModuleServicesScope(provider);

        using var ps = ImportPackedModule();

        var sourceDir = CreateTempDirectory();
        var sourceFile = Path.Combine(sourceDir, "raw-episode.mkv");
        File.Copy(SampleVideoPath, sourceFile);
        var destinationDir = CreateTempDirectory();

        ps.AddCommand("Invoke-SeasonScan")
            .AddParameter("Season", 1)
            .AddParameter("TvDbSeriesUrl", StubSeriesUrl);
        var scanResults = ps.Invoke().ToList();
        var scanErrors = ps.Streams.Error.ReadAll();
        Assert.Empty(scanErrors);
        var episode = Assert.IsType<TvDbEpisodeInfo>(Assert.Single(scanResults).BaseObject);
        ps.Commands.Clear();

        ps.AddCommand("Invoke-VideoCopy")
            .AddParameter("Title", "Component Test Show")
            .AddParameter("Season", 1)
            .AddParameter("Path", new[] { sourceDir })
            .AddParameter("FilePatterns", new[] { "*.mkv" })
            .AddParameter("MinimumFileSize", 1L)
            .AddParameter("Destination", destinationDir)
            .AddParameter("Episodes", new[] { episode });
        var copyResults = ps.Invoke().ToList();
        var copyErrors = ps.Streams.Error.ReadAll();

        Assert.Empty(copyErrors);
        var copiedPath = Assert.IsType<string>(Assert.Single(copyResults).BaseObject);
        Assert.True(File.Exists(copiedPath));
        Assert.Contains("s01e01", Path.GetFileName(copiedPath), StringComparison.OrdinalIgnoreCase);
    }
}
