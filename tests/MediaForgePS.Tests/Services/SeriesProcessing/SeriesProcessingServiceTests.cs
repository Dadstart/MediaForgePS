using System;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Services.TvDb;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.SeriesProcessing;

public class SeriesProcessingServiceTests
{
    private readonly SeriesProcessingService _service;

    public SeriesProcessingServiceTests()
    {
        var logger = new Mock<ILogger<SeriesProcessingService>>();
        var mediaReaderService = new Mock<IMediaReaderService>();
        var executableService = new Mock<IExecutableService>();
        _service = new SeriesProcessingService(
            logger.Object,
            mediaReaderService.Object,
            executableService.Object,
            Mock.Of<ITvDbClient>());
    }

    [Fact]
    public void NormalizeFilePatterns_AddsLeadingAndTrailingWildcard()
    {
        var result = _service.NormalizeFilePatterns(["C4_", "*.mkv", "B3_*"]);

        Assert.Equal(3, result.Count);
        Assert.Equal("*C4_*", result[0]);
        Assert.Equal("*.mkv*", result[1]);
        Assert.Equal("*B3_*", result[2]);
    }

    [Fact]
    public void NormalizeFilePatterns_WhenEmpty_ReturnsEmptyList()
    {
        var result = _service.NormalizeFilePatterns([]);

        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeFilePatterns_WhenContainsWhitespaceOnly_FiltersThemOut()
    {
        var result = _service.NormalizeFilePatterns(["  *.mp4  ", "", "  ", "*.mkv"]);

        Assert.Equal(2, result.Count);
        Assert.Equal("*.mp4*", result[0]);
        Assert.Equal("*.mkv*", result[1]);
    }

    [Fact]
    public void NormalizeFilePatterns_WhenPatternAlreadyHasBothWildcards_LeavesAsIs()
    {
        var result = _service.NormalizeFilePatterns(["*.*"]);

        Assert.Single(result);
        Assert.Equal("*.*", result[0]);
    }

    [Fact]
    public void BuildEpisodeFileName_UsesTvDbMetadataFormat()
    {
        var episode = new TvDbEpisodeInfo("12345", 1, "Pilot", 7);

        var fileName = SeriesProcessingService.BuildEpisodeFileName("The Office", 3, episode, ".mkv");

        Assert.Equal("The Office {tvdb 12345} - s03e07.mkv", fileName);
    }

    [Fact]
    public void BuildEpisodeFileName_FormatsSingleDigitEpisodeWithZeroPadding()
    {
        var episode = new TvDbEpisodeInfo("1", 1, "Pilot", 1);

        var fileName = SeriesProcessingService.BuildEpisodeFileName("Show", 1, episode, ".mkv");

        Assert.Equal("Show {tvdb 1} - s01e01.mkv", fileName);
    }

    [Fact]
    public void BuildEpisodeFileName_ExtensionWithoutLeadingDot_IsIncludedAsIs()
    {
        var episode = new TvDbEpisodeInfo("42", 2, "Title", 5);

        var fileName = SeriesProcessingService.BuildEpisodeFileName("Show", 2, episode, ".mp4");

        Assert.EndsWith(".mp4", fileName);
        Assert.Equal("Show {tvdb 42} - s02e05.mp4", fileName);
    }

    [Theory]
    [InlineData("https://thetvdb.com/series/show/seasons/official", 2, "https://thetvdb.com/series/show/seasons/official/2")]
    [InlineData("https://thetvdb.com/series/show/seasons/official/2", 2, "https://thetvdb.com/series/show/seasons/official/2")]
    [InlineData(null, 2, null)]
    public void EnsureSeasonUrl_NormalizesWhenSeasonSuffixMissing(string? input, int season, string? expected)
    {
        var result = InvokeSeriesProcessingCommand.EnsureSeasonUrl(input, season);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void InvokeSeriesProcessingCommand_DefaultMinimumFileSize_IsOneGigabyte()
    {
        var cmdlet = new InvokeSeriesProcessingCommand();
        Assert.Equal(1L * 1024 * 1024 * 1024, cmdlet.MinimumFileSize);
    }

    [Fact]
    public void InvokeSeriesProcessingCommand_MinimumFileSize_CanBeSetToZeroForBonusContent()
    {
        var cmdlet = new InvokeSeriesProcessingCommand { MinimumFileSize = 0 };
        Assert.Equal(0, cmdlet.MinimumFileSize);
    }

    [Fact]
    public void InvokeSeriesProcessingCommand_UsesOcrParameter()
    {
        var cmdlet = new InvokeSeriesProcessingCommand();

        Assert.Equal(SubtitleOcrMode.Auto, cmdlet.Ocr);
        Assert.NotNull(typeof(InvokeSeriesProcessingCommand).GetProperty(nameof(InvokeSeriesProcessingCommand.Ocr)));
        Assert.Null(typeof(InvokeSeriesProcessingCommand).GetProperty("SkipOcr"));
    }
}
