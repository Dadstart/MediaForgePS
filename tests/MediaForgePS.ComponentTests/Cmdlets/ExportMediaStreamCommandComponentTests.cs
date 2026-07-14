using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class ExportMediaStreamCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void ExportMediaStream_WithVideoStream_WritesOutputFile()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CopySampleVideoAs("export-in.mkv");
        var outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, "export-video.mkv");

        using var ps = CreatePowerShellFor<ExportMediaStreamCommand>("Export-MediaStream");
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("Type", "Video")
            .AddParameter("Index", 0)
            .AddParameter("Force", true);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Empty(results);
        Assert.True(File.Exists(outputPath), $"Expected exported stream at {outputPath}");
        Assert.True(new FileInfo(outputPath).Length > 0);
    }
}
