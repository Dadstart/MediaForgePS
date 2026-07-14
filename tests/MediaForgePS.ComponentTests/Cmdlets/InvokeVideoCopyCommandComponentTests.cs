using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class InvokeVideoCopyCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void InvokeVideoCopy_WithTinyEpisodeFile_CopiesAndNamesDestination()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var sourceDir = CreateTempDirectory();
        var sourceFile = Path.Combine(sourceDir, "raw-episode.mkv");
        File.Copy(SampleVideoPath, sourceFile);

        var destinationDir = CreateTempDirectory();
        var episodes = new[]
        {
            new TvDbEpisodeInfo("12345", 1, "Pilot", 1)
        };

        using var ps = CreatePowerShellFor<InvokeVideoCopyCommand>("Invoke-VideoCopy");
        ps.AddCommand("Invoke-VideoCopy")
            .AddParameter("Title", "Test Show")
            .AddParameter("Season", 1)
            .AddParameter("Path", new[] { sourceDir })
            .AddParameter("FilePatterns", new[] { "*.mkv" })
            .AddParameter("MinimumFileSize", 1L)
            .AddParameter("Destination", destinationDir)
            .AddParameter("Episodes", episodes);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var copiedPath = Assert.IsType<string>(Assert.Single(results).BaseObject);
        Assert.True(File.Exists(copiedPath), $"Expected copied file at {copiedPath}");
        Assert.Equal(destinationDir, Path.GetDirectoryName(copiedPath));
        Assert.Contains("Test Show", Path.GetFileName(copiedPath), System.StringComparison.Ordinal);
        Assert.Contains("s01e01", Path.GetFileName(copiedPath), System.StringComparison.OrdinalIgnoreCase);
        Assert.True(new FileInfo(copiedPath).Length > 0);
    }
}
