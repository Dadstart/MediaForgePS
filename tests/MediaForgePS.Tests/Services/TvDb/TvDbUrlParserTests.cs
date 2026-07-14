using Dadstart.Labs.MediaForge.Services.TvDb;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.TvDb;

public class TvDbUrlParserTests
{
    [Theory]
    [InlineData("https://thetvdb.com/series/12345", "12345")]
    [InlineData("https://thetvdb.com/series/breaking-bad", "breaking-bad")]
    [InlineData("https://thetvdb.com/series/breaking-bad/", "breaking-bad")]
    public void TryParseSeriesUrl_ExtractsSeriesKey(string url, string expectedKey)
    {
        Assert.True(TvDbUrlParser.TryParseSeriesUrl(url, out var seriesKey));
        Assert.Equal(expectedKey, seriesKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/series/1")]
    [InlineData("https://thetvdb.com/series/")]
    public void TryParseSeriesUrl_WhenInvalid_ReturnsFalse(string? url)
    {
        Assert.False(TvDbUrlParser.TryParseSeriesUrl(url, out _));
    }

    [Fact]
    public void TryParseSeasonUrl_ExtractsTypeAndSeason()
    {
        Assert.True(TvDbUrlParser.TryParseSeasonUrl(
            "https://thetvdb.com/series/breaking-bad/seasons/dvd/2",
            out var parsed));

        Assert.Equal("breaking-bad", parsed.SeriesKey);
        Assert.Equal("dvd", parsed.SeasonType);
        Assert.Equal(2, parsed.SeasonNumber);
    }

    [Fact]
    public void TryParseSeasonUrl_WhenSeasonMissing_LeavesSeasonNull()
    {
        Assert.True(TvDbUrlParser.TryParseSeasonUrl(
            "https://thetvdb.com/series/12345/seasons/official",
            out var parsed));

        Assert.Equal("12345", parsed.SeriesKey);
        Assert.Equal("official", parsed.SeasonType);
        Assert.Null(parsed.SeasonNumber);
    }

    [Theory]
    [InlineData("https://thetvdb.com/series/show")]
    [InlineData("https://thetvdb.com/series/show/seasons")]
    [InlineData("https://thetvdb.com/series/show/seasons/official/zero")]
    public void TryParseSeasonUrl_WhenInvalid_ReturnsFalse(string url)
    {
        Assert.False(TvDbUrlParser.TryParseSeasonUrl(url, out _));
    }
}
