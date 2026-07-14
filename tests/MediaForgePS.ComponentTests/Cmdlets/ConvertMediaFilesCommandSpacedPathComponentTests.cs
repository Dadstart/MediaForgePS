using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class ConvertMediaFilesCommandSpacedPathComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void ConvertMediaFiles_WithInputPathContainingSpaces_ProducesOutputFile()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CopySampleVideoAs("sample with spaces.mkv");
        var outputDir = Path.Combine(Path.GetDirectoryName(inputPath)!, "out folder");
        Directory.CreateDirectory(outputDir);
        var expectedOutput = Path.Combine(outputDir, "sample with spaces.mp4");

        using var ps = CreatePowerShellFor<ConvertMediaFilesCommand>("Convert-MediaFiles");
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { inputPath })
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264");

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.True(File.Exists(expectedOutput), $"Expected output at {expectedOutput}");
        Assert.True(new FileInfo(expectedOutput).Length > 0);

        var conversionResults = Assert.IsAssignableFrom<IEnumerable<MediaConversionResult>>(
            Assert.Single(results).BaseObject);
        var result = Assert.Single(conversionResults);
        AssertSuccessfulConversionResult(result, inputPath, expectedOutput);
    }
}
