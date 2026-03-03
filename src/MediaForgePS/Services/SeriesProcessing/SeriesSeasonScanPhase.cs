using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text.RegularExpressions;
using Dadstart.Labs.MediaForge.Models;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

internal sealed class SeriesSeasonScanPhase(ILogger logger)
{
    private const string TvDbSeriesUrlPrefix = "https://thetvdb.com/series/";
    private const string TvDbSeasonPathSegment = "/seasons/";

    private static readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "MediaForgePS/1.0" } }
    };

    private static readonly Regex _episodeIdRegex = new(@"/series/[^/]+/episodes/(\d+)", RegexOptions.Compiled);

    public IReadOnlyList<TvDbEpisodeInfo> Run(PSCmdlet cmdlet, int season, string? tvDbSeriesUrl, string? tvDbSeasonUrl)
    {
        if (string.IsNullOrWhiteSpace(tvDbSeasonUrl) && string.IsNullOrWhiteSpace(tvDbSeriesUrl))
        {
            cmdlet.WriteError(new ErrorRecord(
                new ArgumentException("Either TvDbSeriesUrl or TvDbSeasonUrl must be provided."),
                "TvDbUrlMissing",
                ErrorCategory.InvalidArgument,
                null));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        if (!string.IsNullOrWhiteSpace(tvDbSeriesUrl) &&
            !tvDbSeriesUrl.StartsWith(TvDbSeriesUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            cmdlet.WriteError(new ErrorRecord(
                new ArgumentException($"Invalid TVDb URL format. Expected: {TvDbSeriesUrlPrefix}show-name"),
                "InvalidTvDbUrl",
                ErrorCategory.InvalidArgument,
                tvDbSeriesUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        if (!string.IsNullOrWhiteSpace(tvDbSeasonUrl) &&
            (!tvDbSeasonUrl.StartsWith(TvDbSeriesUrlPrefix, StringComparison.OrdinalIgnoreCase) ||
             !tvDbSeasonUrl.Contains(TvDbSeasonPathSegment, StringComparison.Ordinal)))
        {
            cmdlet.WriteError(new ErrorRecord(
                new ArgumentException($"Invalid TVDb season URL format. Expected: {TvDbSeriesUrlPrefix}show-name/seasons/..."),
                "InvalidTvDbSeasonUrl",
                ErrorCategory.InvalidArgument,
                tvDbSeasonUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        var seasonUrl = !string.IsNullOrWhiteSpace(tvDbSeasonUrl)
            ? tvDbSeasonUrl
            : $"{tvDbSeriesUrl!.TrimEnd('/')}/seasons/official/{season}";

        logger.LogDebug("Fetching TVDb season page: {SeasonUrl}", seasonUrl);

        string html;
        try
        {
            html = _httpClient.GetStringAsync(seasonUrl).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            cmdlet.WriteError(new ErrorRecord(ex, "TvDbRequestFailed", ErrorCategory.ConnectionError, seasonUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        var episodeIds = _episodeIdRegex.Matches(html)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .OrderBy(id => int.Parse(id, CultureInfo.InvariantCulture))
            .ToList();

        if (episodeIds.Count == 0)
        {
            logger.LogDebug("No episode IDs found on the season page");
            return Array.Empty<TvDbEpisodeInfo>();
        }

        var episodes = new List<TvDbEpisodeInfo>(episodeIds.Count);
        for (var index = 0; index < episodeIds.Count; index++)
        {
            var episodeId = episodeIds[index];
            var titlePattern = $"episodes/{Regex.Escape(episodeId)}[^>]*>([^<]+)</a>";
            var titleMatch = Regex.Match(html, titlePattern);
            var title = titleMatch.Success
                ? titleMatch.Groups[1].Value.Trim()
                : $"Episode {index + 1}";

            episodes.Add(new TvDbEpisodeInfo(episodeId, season, title, index + 1));
        }

        logger.LogDebug("Found {Count} episodes for season {Season}", episodes.Count, season);
        return episodes;
    }
}
