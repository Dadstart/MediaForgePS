using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class ExportSubtitlesCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void ExportSubtitles_WithEnglishSrtTrack_WritesSubtitleProcessingResult()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CreateSampleVideoWithEnglishSubtitles("export-subs.mkv");
        var expectedSrt = Path.Combine(Path.GetDirectoryName(inputPath)!, "export-subs.eng.sdh.srt");

        using var ps = CreatePowerShellFor<ExportSubtitlesCommand>("Export-Subtitles");
        ps.AddCommand("Export-Subtitles")
            .AddParameter("InputPath", inputPath)
            .AddParameter("Ocr", SubtitleOcrMode.Skip);

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results));
        Assert.Equal(1, result.ExtractedCount);
        Assert.Equal(0, result.ConvertedCount);
        Assert.Contains(
            result.ExtractedPaths,
            path => string.Equals(path, expectedSrt, System.StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(expectedSrt), $"Expected extracted SRT at {expectedSrt}");
    }
}
