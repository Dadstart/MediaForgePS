using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.E2ETests.Cmdlets;

public class ConversionStatisticsE2ETests : E2ETestBase
{
    [Fact(Timeout = 180_000)]
    public void PackedModule_ConvertVideoFile_WritesAverageStatisticsForBatch()
    {
        using var ps = ImportPackedModule();

        var inputDir = CreateTempDirectory();
        var outputDir = CreateTempDirectory();
        File.Copy(SampleVideoPath, Path.Combine(inputDir, "episode-one.mkv"));
        File.Copy(SampleVideoPath, Path.Combine(inputDir, "episode-two.mkv"));

        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", inputDir)
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264")
            .AddParameter("SkipSubtitles");

        var results = ps.Invoke().Select(r => r.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var conversions = results.OfType<MediaConversionResult>().ToList();
        Assert.Equal(2, conversions.Count);
        Assert.All(conversions, c => Assert.Equal(MediaConversionResult.CompletedStatus, c.Status));

        var statistics = Assert.Single(results.OfType<MediaConversionStatistics>());
        Assert.Equal(2, statistics.FileCount);
        Assert.NotNull(statistics.AverageSizeReductionPercent);
        Assert.Equal(
            Math.Round(conversions.Average(c => c.InputSizeMegabytes), 1),
            statistics.AverageInputSizeMegabytes);
        Assert.Equal(
            Math.Round(conversions.Average(c => c.OutputSizeMegabytes), 1),
            statistics.AverageOutputSizeMegabytes);
        Assert.Equal(
            Math.Round(conversions.Average(c => c.SizeReductionPercent!.Value), 1),
            statistics.AverageSizeReductionPercent!.Value,
            precision: 1);
        Assert.True(statistics.AverageProcessingTime > TimeSpan.Zero);
    }

    [Fact(Timeout = 180_000)]
    public void PackedModule_ConversionStatistics_RenderWithFormatView()
    {
        using var ps = ImportPackedModule();

        var inputDir = CreateTempDirectory();
        var outputDir = CreateTempDirectory();
        File.Copy(SampleVideoPath, Path.Combine(inputDir, "episode-one.mkv"));
        File.Copy(SampleVideoPath, Path.Combine(inputDir, "episode-two.mkv"));

        ps.AddCommand("Convert-VideoFile")
            .AddParameter("InputPath", inputDir)
            .AddParameter("OutputDirectory", outputDir)
            .AddParameter("DefaultVideoEncoder", "x264")
            .AddParameter("SkipSubtitles");
        ps.AddCommand("Where-Object")
            .AddParameter("FilterScript", System.Management.Automation.ScriptBlock.Create(
                "$_ -is [Dadstart.Labs.MediaForge.Models.MediaConversionStatistics]"));
        ps.AddCommand("Out-String");

        var rendered = string.Join(
            Environment.NewLine,
            ps.Invoke().Select(r => r.BaseObject?.ToString()));
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Contains("AvgSizeReduction", rendered, StringComparison.Ordinal);
        Assert.Contains("AvgInputSize", rendered, StringComparison.Ordinal);
        Assert.Contains("AvgOutputSize", rendered, StringComparison.Ordinal);
        Assert.Contains("AvgDuration", rendered, StringComparison.Ordinal);
        Assert.True(
            rendered.Contains("smaller", StringComparison.Ordinal) ||
            rendered.Contains("larger", StringComparison.Ordinal) ||
            rendered.Contains("same size", StringComparison.Ordinal),
            $"Expected a size-change phrase in rendered statistics: {rendered}");
    }
}
