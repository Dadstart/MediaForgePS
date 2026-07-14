using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.TvDb;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

internal sealed class SeriesSeasonScanPhase(ITvDbClient tvDbClient, ILogger logger)
{
    public IReadOnlyList<TvDbEpisodeInfo> Run(ICmdletErrorSink errors, int season, string? tvDbSeriesUrl, string? tvDbSeasonUrl)
    {
        if (string.IsNullOrWhiteSpace(tvDbSeasonUrl) && string.IsNullOrWhiteSpace(tvDbSeriesUrl))
        {
            errors.WriteError(new ErrorRecord(
                new ArgumentException("Either TvDbSeriesUrl or TvDbSeasonUrl must be provided."),
                "TvDbUrlMissing",
                ErrorCategory.InvalidArgument,
                null));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        if (!string.IsNullOrWhiteSpace(tvDbSeriesUrl) && !TvDbUrlParser.IsSeriesUrl(tvDbSeriesUrl))
        {
            errors.WriteError(new ErrorRecord(
                new ArgumentException($"Invalid TVDb URL format. Expected: {TvDbUrlParser.SeriesUrlPrefix}show-name"),
                "InvalidTvDbUrl",
                ErrorCategory.InvalidArgument,
                tvDbSeriesUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        if (!TryResolveScanTarget(tvDbSeriesUrl, tvDbSeasonUrl, season, out var seriesKey, out var seasonType, out var effectiveSeason, out var urlError))
        {
            errors.WriteError(urlError!);
            return Array.Empty<TvDbEpisodeInfo>();
        }

        try
        {
            logger.LogDebug(
                "Resolving TVDb series '{SeriesKey}' for season {Season} ({SeasonType})",
                seriesKey,
                effectiveSeason,
                seasonType);

            var seriesId = tvDbClient.ResolveSeriesIdAsync(seriesKey).ConfigureAwait(false).GetAwaiter().GetResult();
            var episodes = tvDbClient
                .GetSeasonEpisodesAsync(seriesId, effectiveSeason, seasonType)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            logger.LogDebug("Found {Count} episodes for season {Season}", episodes.Count, effectiveSeason);
            return episodes;
        }
        catch (TvDbApiException ex)
        {
            errors.WriteError(new ErrorRecord(ex, ex.ErrorId, MapErrorCategory(ex.ErrorId), tvDbSeasonUrl ?? tvDbSeriesUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }
        catch (HttpRequestException ex)
        {
            errors.WriteError(new ErrorRecord(
                ex,
                "TvDbRequestFailed",
                ErrorCategory.ConnectionError,
                tvDbSeasonUrl ?? tvDbSeriesUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }
    }

    private static bool TryResolveScanTarget(
        string? tvDbSeriesUrl,
        string? tvDbSeasonUrl,
        int season,
        out string seriesKey,
        out string seasonType,
        out int effectiveSeason,
        out ErrorRecord? error)
    {
        seriesKey = string.Empty;
        seasonType = TvDbUrlParser.DefaultSeasonType;
        effectiveSeason = season;
        error = null;

        if (!string.IsNullOrWhiteSpace(tvDbSeasonUrl))
        {
            if (!TvDbUrlParser.TryParseSeasonUrl(tvDbSeasonUrl, out var parsedSeason))
            {
                error = new ErrorRecord(
                    new ArgumentException(
                        $"Invalid TVDb season URL format. Expected: {TvDbUrlParser.SeriesUrlPrefix}show-name/seasons/official/1"),
                    "InvalidTvDbSeasonUrl",
                    ErrorCategory.InvalidArgument,
                    tvDbSeasonUrl);
                return false;
            }

            seriesKey = parsedSeason.SeriesKey;
            seasonType = parsedSeason.SeasonType;
            if (parsedSeason.SeasonNumber is int seasonFromUrl)
                effectiveSeason = seasonFromUrl;

            return true;
        }

        if (!TvDbUrlParser.TryParseSeriesUrl(tvDbSeriesUrl, out seriesKey))
        {
            error = new ErrorRecord(
                new ArgumentException($"Invalid TVDb URL format. Expected: {TvDbUrlParser.SeriesUrlPrefix}show-name"),
                "InvalidTvDbUrl",
                ErrorCategory.InvalidArgument,
                tvDbSeriesUrl);
            return false;
        }

        return true;
    }

    private static ErrorCategory MapErrorCategory(string errorId) => errorId switch
    {
        "TvDbApiKeyMissing" => ErrorCategory.InvalidOperation,
        "TvDbAuthFailed" => ErrorCategory.AuthenticationError,
        "TvDbSeriesNotFound" => ErrorCategory.ObjectNotFound,
        "TvDbInvalidResponse" => ErrorCategory.InvalidData,
        _ => ErrorCategory.ConnectionError
    };
}
