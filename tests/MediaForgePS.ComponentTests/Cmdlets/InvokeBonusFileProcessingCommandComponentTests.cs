using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class InvokeBonusFileProcessingCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void InvokeBonusFileProcessing_WithTrailerSample_WritesConversionResultWithSizeAndDuration()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CopySampleVideoAs("clip-trailer.mkv");
        var inputDir = Path.GetDirectoryName(inputPath)!;
        var outputDir = CreateTempDirectory();
        var expectedConversionOutput = Path.Combine(inputDir, "clip-trailer.mp4");
        var expectedPlexDestination = Path.Combine(outputDir, "Trailers", "clip-trailer.mp4");

        using var ps = CreatePowerShellFor<InvokeBonusFileProcessingCommand>("Invoke-BonusFileProcessing");
        ps.AddCommand("Invoke-BonusFileProcessing")
            .AddParameter("InputPath", inputDir)
            .AddParameter("OutputPath", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264")
            .AddParameter("SkipSubtitles");

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<MediaConversionResult>(Assert.Single(results).BaseObject);
        AssertSuccessfulConversionResult(
            result,
            inputPath,
            expectedConversionOutput,
            requireOutputFileExists: false);

        Assert.True(
            File.Exists(expectedPlexDestination),
            $"Expected moved bonus file at {expectedPlexDestination}");
        Assert.True(new FileInfo(expectedPlexDestination).Length > 0);
    }

    [Fact(Timeout = 60_000)]
    public void InvokeBonusFileProcessing_WithEnglishSubtitles_WritesSubtitleProcessingResult()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CreateSampleVideoWithEnglishSubtitles("clip-trailer.mkv");
        var inputDir = Path.GetDirectoryName(inputPath)!;
        var outputDir = CreateTempDirectory();

        using var ps = CreatePowerShellFor<InvokeBonusFileProcessingCommand>("Invoke-BonusFileProcessing");
        ps.AddCommand("Invoke-BonusFileProcessing")
            .AddParameter("InputPath", inputDir)
            .AddParameter("OutputPath", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264")
            .AddParameter("Ocr", SubtitleOcrMode.Skip);

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Single(results.OfType<MediaConversionResult>());
        var subtitles = Assert.Single(results.OfType<SubtitleProcessingResult>());
        Assert.Equal(1, subtitles.ExtractedCount);
        Assert.Equal(0, subtitles.ConvertedCount);
        Assert.NotEmpty(subtitles.ExtractedPaths);
    }
}
