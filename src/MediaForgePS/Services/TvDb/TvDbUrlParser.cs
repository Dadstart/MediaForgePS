using System;
using System.Globalization;

namespace Dadstart.Labs.MediaForge.Services.TvDb;

/// <summary>
/// Parses TheTVDB website URLs into series keys and season order metadata.
/// </summary>
internal static class TvDbUrlParser
{
    public const string SeriesUrlPrefix = "https://thetvdb.com/series/";
    public const string DefaultSeasonType = "official";

    public sealed record ParsedSeasonUrl(string SeriesKey, string SeasonType, int? SeasonNumber);

    public static bool IsSeriesUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        url.StartsWith(SeriesUrlPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool TryParseSeriesUrl(string? url, out string seriesKey)
    {
        seriesKey = string.Empty;
        if (!IsSeriesUrl(url))
            return false;

        var remainder = url![SeriesUrlPrefix.Length..].Trim('/');
        if (string.IsNullOrWhiteSpace(remainder))
            return false;

        var slashIndex = remainder.IndexOf('/');
        seriesKey = slashIndex < 0 ? remainder : remainder[..slashIndex];
        return !string.IsNullOrWhiteSpace(seriesKey);
    }

    public static bool TryParseSeasonUrl(string? url, out ParsedSeasonUrl parsed)
    {
        parsed = null!;
        if (!IsSeriesUrl(url))
            return false;

        var remainder = url![SeriesUrlPrefix.Length..].Trim('/');
        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3 ||
            !segments[1].Equals("seasons", StringComparison.OrdinalIgnoreCase))
            return false;

        var seriesKey = segments[0];
        var seasonType = segments[2];
        if (string.IsNullOrWhiteSpace(seriesKey) || string.IsNullOrWhiteSpace(seasonType))
            return false;

        int? seasonNumber = null;
        if (segments.Length >= 4)
        {
            if (!int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSeason) ||
                parsedSeason <= 0)
                return false;

            seasonNumber = parsedSeason;
        }

        parsed = new ParsedSeasonUrl(seriesKey, seasonType.ToLowerInvariant(), seasonNumber);
        return true;
    }
}
