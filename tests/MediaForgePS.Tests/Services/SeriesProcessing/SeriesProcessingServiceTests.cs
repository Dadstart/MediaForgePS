using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
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
        _service = new SeriesProcessingService(logger.Object, mediaReaderService.Object, executableService.Object);
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
    public void BuildEpisodeFileName_UsesTvDbMetadataFormat()
    {
        var episode = new TvDbEpisodeInfo("12345", 1, "Pilot", 7);

        var fileName = SeriesProcessingService.BuildEpisodeFileName("The Office", 3, episode, ".mkv");

        Assert.Equal("The Office {tvdb 12345} - s03e07.mkv", fileName);
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
}
