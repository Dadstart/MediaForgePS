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
        var result = Assert.IsType<MediaConversionResult>(
            Assert.Single(results.Select(r => r.BaseObject).OfType<MediaConversionResult>()));
        AssertSuccessfulConversionResult(result, inputPath, expectedOutput);

        var statistics = Assert.Single(results.Select(r => r.BaseObject).OfType<MediaConversionStatistics>());
        Assert.Equal(1, statistics.FileCount);
        Assert.Equal(result.SizeReductionPercent, statistics.AverageSizeReductionPercent);
        Assert.Equal(result.InputSizeBytes, statistics.AverageInputSizeBytes);
        Assert.Equal(result.OutputSizeBytes, statistics.AverageOutputSizeBytes);
    }

    [Fact(Timeout = 120_000)]
    public void ConvertVideoFile_WithMultipleFiles_WritesAverageStatistics()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputDir = CreateTempDirectory();
        var outputDir = CreateTempDirectory();
        File.Copy(SampleVideoPath, Path.Combine(inputDir, "episode-one.mkv"));
        File.Copy(SampleVideoPath, Path.Combine(inputDir, "episode-two.mkv"));

        using var ps = CreatePowerShellFor<ConvertVideoFileCommand>("Convert-VideoFile");
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", inputDir)
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264")
            .AddParameter("SkipSubtitles");

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var conversions = results.OfType<MediaConversionResult>().ToList();
        Assert.Equal(2, conversions.Count);
        Assert.All(conversions, c => Assert.Equal(MediaConversionResult.CompletedStatus, c.Status));

        var statistics = Assert.Single(results.OfType<MediaConversionStatistics>());
        Assert.Equal(2, statistics.FileCount);
        Assert.Equal(
            (long)Math.Round(conversions.Average(c => c.InputSizeBytes)),
            statistics.AverageInputSizeBytes);
        Assert.Equal(
            (long)Math.Round(conversions.Average(c => c.OutputSizeBytes)),
            statistics.AverageOutputSizeBytes);
        Assert.NotNull(statistics.AverageSizeReductionPercent);
        Assert.Equal(
            Math.Round(conversions.Average(c => c.SizeReductionPercent!.Value), 1),
            statistics.AverageSizeReductionPercent!.Value,
            precision: 1);
        Assert.True(statistics.AverageProcessingTime > TimeSpan.Zero);
    }

    [Fact(Timeout = 60_000)]
    public void ConvertVideoFile_WithEnglishSubtitles_WritesSubtitleProcessingResult()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CreateSampleVideoWithEnglishSubtitles("episode-subs.mkv");
        var outputDir = CreateTempDirectory();
        var expectedOutput = Path.Combine(outputDir, "episode-subs.mp4");

        using var ps = CreatePowerShellFor<ConvertVideoFileCommand>("Convert-VideoFile");
        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264")
            .AddParameter("Ocr", SubtitleOcrMode.Skip);

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var conversion = Assert.Single(results.OfType<MediaConversionResult>());
        AssertSuccessfulConversionResult(conversion, inputPath, expectedOutput);

        var statistics = Assert.Single(results.OfType<MediaConversionStatistics>());
        Assert.Equal(1, statistics.FileCount);

        var subtitles = Assert.Single(results.OfType<SubtitleProcessingResult>());
        Assert.Equal(1, subtitles.ExtractedCount);
        Assert.Equal(0, subtitles.ConvertedCount);
        Assert.NotEmpty(subtitles.ExtractedPaths);
        Assert.All(subtitles.ExtractedPaths, path => Assert.True(File.Exists(path), $"Missing extracted path: {path}"));
    }
}
