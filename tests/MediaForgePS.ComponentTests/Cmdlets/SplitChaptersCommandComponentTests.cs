using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class SplitChaptersCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void SplitChapters_AllChapters_WritesSplitOutputFiles()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CreateSampleVideoWithChapters("chapters-in.mkv");
        var outputDir = CreateTempDirectory();

        using var ps = CreatePowerShellFor<SplitChaptersCommand>("Split-Chapters");
        ps.AddCommand("Split-Chapters")
            .AddParameter("InputFile", inputPath)
            .AddParameter("AllChapters")
            .AddParameter("OutputPath", outputDir);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.NotEmpty(results);

        var outputPaths = results.Select(r => Assert.IsType<string>(r.BaseObject)).ToList();
        Assert.True(outputPaths.Count >= 2, $"Expected at least 2 chapter outputs; got {outputPaths.Count}");
        Assert.All(outputPaths, path =>
        {
            Assert.True(File.Exists(path), $"Expected split output at {path}");
            Assert.True(new FileInfo(path).Length > 0);
        });
    }
}
