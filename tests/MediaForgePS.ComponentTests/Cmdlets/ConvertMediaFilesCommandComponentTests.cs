using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
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

        using var ps = CreatePowerShellFor<ConvertMediaFilesCommand>("Convert-MediaFiles");
        ps.AddCommand("Convert-MediaFiles")
            .AddParameter("InputPath", new object[] { SampleVideoPath })
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264");

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);

        var outputFiles = Directory.GetFiles(outputDir);
        Assert.Single(outputFiles);
        var outputFile = outputFiles[0];
        Assert.True(new FileInfo(outputFile).Length > 0);
    }
}
