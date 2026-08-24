using System;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.ComponentTests.Infrastructure;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class SeriesProcessingPipelineComponentTests : ComponentTestBase
{
    private const string StubSeriesUrl = "https://thetvdb.com/series/component-test-show";

    [Fact(Timeout = 120_000)]
    public void SeriesPipeline_StubbedSeasonScanAndVideoCopy_ProducesNamedEpisodeFile()
    {
        SkipIfTestAssetsMissing();

        var provider = ComponentServiceProviderFactory.CreateWithStubTvDb(
            new StubTvDbClient(StubTvDbClient.DefaultSeasonOneEpisodes[0]));
        using var moduleScope = new ComponentModuleServicesScope(provider);

        var sourceDir = CreateTempDirectory();
        var sourceFile = Path.Combine(sourceDir, "raw-episode.mkv");
        File.Copy(SampleVideoPath, sourceFile);

        var destinationDir = CreateTempDirectory();
        TvDbEpisodeInfo[] scannedEpisodes;

        using (var scanPs = CreatePowerShellFor<InvokeSeasonScanCommand>("Invoke-SeasonScan"))
        {
            scanPs.AddCommand("Invoke-SeasonScan")
                .AddParameter("Season", 1)
                .AddParameter("TvDbSeriesUrl", StubSeriesUrl);

            var scanResults = scanPs.Invoke().ToList();
            var scanErrors = scanPs.Streams.Error.ReadAll();

            Assert.Empty(scanErrors);
            scannedEpisodes = scanResults.Select(r => Assert.IsType<TvDbEpisodeInfo>(r.BaseObject)).ToArray();
            var episode = Assert.Single(scannedEpisodes);
            Assert.Equal("1001", episode.Id);
            Assert.Equal("Pilot", episode.Title);
        }

        using var copyPs = CreatePowerShellFor<InvokeVideoCopyCommand>("Invoke-VideoCopy");
        copyPs.AddCommand("Invoke-VideoCopy")
            .AddParameter("Title", "Component Test Show")
            .AddParameter("Season", 1)
            .AddParameter("Path", new[] { sourceDir })
            .AddParameter("FilePatterns", new[] { "*.mkv" })
            .AddParameter("MinimumFileSize", 1L)
            .AddParameter("Destination", destinationDir)
            .AddParameter("Episodes", scannedEpisodes);

        var copyResults = copyPs.Invoke().ToList();
        var copyErrors = copyPs.Streams.Error.ReadAll();

        Assert.Empty(copyErrors);
        var copiedPath = Assert.IsType<string>(Assert.Single(copyResults).BaseObject);
        Assert.True(File.Exists(copiedPath), $"Expected copied episode at {copiedPath}");
        Assert.Equal(destinationDir, Path.GetDirectoryName(copiedPath));
        Assert.Contains("Component Test Show", Path.GetFileName(copiedPath), StringComparison.Ordinal);
        Assert.Contains("s01e01", Path.GetFileName(copiedPath), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{tvdb 1001}", Path.GetFileName(copiedPath), StringComparison.Ordinal);
        Assert.True(new FileInfo(copiedPath).Length > 0);
    }

    [Fact(Timeout = 120_000)]
    public void SeriesPipeline_StubbedSeasonScanAndSplitSeriesChapters_WritesEpisodeOutputs()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var provider = ComponentServiceProviderFactory.CreateWithStubTvDb(
            new StubTvDbClient(StubTvDbClient.DefaultSeasonOneEpisodes));
        using var moduleScope = new ComponentModuleServicesScope(provider);

        var inputPath = CreateSampleVideoWithChapters("season-disc.mkv");
        var outputDir = CreateTempDirectory();

        using var ps = CreatePowerShellFor<SplitSeriesChaptersCommand>("Split-SeriesChapters");
        ps.AddCommand("Split-SeriesChapters")
            .AddParameter("Title", "Component Test Show")
            .AddParameter("Season", 1)
            .AddParameter("InputFile", inputPath)
            .AddParameter("ChapterRanges", new object[]
            {
                new ChapterRange(1, 1),
                new ChapterRange(2, 2)
            })
            .AddParameter("OutputPath", outputDir)
            .AddParameter("TvDbSeriesUrl", StubSeriesUrl);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Equal(2, results.Count);

        var outputPaths = results.Select(r => Assert.IsType<string>(r.BaseObject)).ToList();
        Assert.All(outputPaths, path =>
        {
            Assert.True(File.Exists(path), $"Expected split output at {path}");
            Assert.StartsWith(outputDir, path, StringComparison.OrdinalIgnoreCase);
            Assert.True(new FileInfo(path).Length > 0);
        });

        Assert.Contains(outputPaths, path => Path.GetFileName(path).Contains("{tvdb 1001}", StringComparison.Ordinal));
        Assert.Contains(outputPaths, path => Path.GetFileName(path).Contains("{tvdb 1002}", StringComparison.Ordinal));
    }
}
