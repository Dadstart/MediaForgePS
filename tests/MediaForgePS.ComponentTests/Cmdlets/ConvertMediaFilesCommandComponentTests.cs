using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class ConvertMediaFilesCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void ConvertMediaFiles_WithValidSampleVideo_ProducesOutputFileInOutputDirectory()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var outputDir = CreateTempDirectory();
        var expectedOutput = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(SampleVideoPath) + ".mp4");

        using var ps = CreatePowerShellFor<ConvertMediaFilesCommand>("Convert-MediaFiles");
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { SampleVideoPath })
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264");

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.True(File.Exists(expectedOutput));
        Assert.True(new FileInfo(expectedOutput).Length > 0);

        var conversionResults = Assert.IsAssignableFrom<IEnumerable<MediaConversionResult>>(
            Assert.Single(results.Select(r => r.BaseObject).OfType<IEnumerable<MediaConversionResult>>()));
        var result = Assert.Single(conversionResults);
        AssertSuccessfulConversionResult(result, SampleVideoPath, expectedOutput);

        var statistics = Assert.Single(results.Select(r => r.BaseObject).OfType<MediaConversionStatistics>());
        Assert.Equal(1, statistics.FileCount);
        Assert.Equal(result.SizeReductionPercent, statistics.AverageSizeReductionPercent);
        Assert.Equal(result.InputSizeBytes, statistics.AverageInputSizeBytes);
        Assert.Equal(result.OutputSizeBytes, statistics.AverageOutputSizeBytes);
    }
}
