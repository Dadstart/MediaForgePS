using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class ConvertVideoFileCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void ConvertVideoFile_WithValidSampleVideo_WritesConversionResultWithSizeAndDuration()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CopySampleVideoAs("episode.mkv");
        var outputDir = CreateTempDirectory();
        var expectedOutput = Path.Combine(outputDir, "episode.mp4");

        using var ps = CreatePowerShellFor<ConvertVideoFileCommand>("Convert-VideoFile");
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264")
            .AddParameter("SkipSubtitles");

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<MediaConversionResult>(Assert.Single(results).BaseObject);
        AssertSuccessfulConversionResult(result, inputPath, expectedOutput);
    }
}
