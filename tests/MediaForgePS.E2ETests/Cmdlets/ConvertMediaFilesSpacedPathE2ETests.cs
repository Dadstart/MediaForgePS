using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.E2ETests.Cmdlets;

public class ConvertMediaFilesSpacedPathE2ETests : E2ETestBase
{
    [Fact(Timeout = 180_000)]
    public void PackedModule_ConvertMediaFiles_WithSpacedPaths_Succeeds()
    {
        using var ps = ImportPackedModule();

        var workDir = CreateTempDirectory();
        var inputDir = Path.Combine(workDir, "source media");
        var outputDir = Path.Combine(workDir, "export folder");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        var inputPath = Path.Combine(inputDir, "sample video.mkv");
        File.Copy(SampleVideoPath, inputPath);
        var expectedOutputPath = Path.Combine(outputDir, "sample video.mp4");

        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264");

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.True(File.Exists(expectedOutputPath), $"Expected output at {expectedOutputPath}");
        Assert.True(new FileInfo(expectedOutputPath).Length > 0);

        var conversionResults = Assert.IsAssignableFrom<IEnumerable<MediaConversionResult>>(
            Assert.Single(results.Select(r => r.BaseObject).OfType<IEnumerable<MediaConversionResult>>()));
        var result = Assert.Single(conversionResults);
        Assert.Equal(MediaConversionResult.CompletedStatus, result.Status);
        Assert.True(
            string.Equals(result.OutputPath, expectedOutputPath, StringComparison.OrdinalIgnoreCase),
            $"OutputPath mismatch: {result.OutputPath}");

        Assert.Empty(results.Select(r => r.BaseObject).OfType<MediaConversionStatistics>());
    }
}
