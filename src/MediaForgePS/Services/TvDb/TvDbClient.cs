using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.TvDb;

/// <summary>
/// TheTVDB API v4 client. Authenticates with credentials from <see cref="ITvDbCredentialProvider"/>.
/// </summary>
public sealed class TvDbClient : ITvDbClient, IDisposable
{
    private const string ApiBaseAddress = "https://api4.thetvdb.com/v4/";
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);

    private readonly ITvDbCredentialProvider _credentials;
    private readonly ILogger<TvDbClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private string? _bearerToken;
    private bool _disposed;

    public TvDbClient(ITvDbCredentialProvider credentials, ILogger<TvDbClient> logger)
        : this(credentials, logger, new HttpClientHandler(), disposeHandler: true)
    {
    }

    internal TvDbClient(
        ITvDbCredentialProvider credentials,
        ILogger<TvDbClient> logger,
        HttpMessageHandler handler,
        bool disposeHandler)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(handler);

        _credentials = credentials;
        _logger = logger;
        _httpClient = new HttpClient(handler, disposeHandler)
        {
            BaseAddress = new Uri(ApiBaseAddress),
            Timeout = _defaultTimeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"MediaForgePS/{GetAssemblyVersion()}");
    }

    private static string GetAssemblyVersion()
    {
        var assembly = typeof(TvDbClient).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
            return plusIndex >= 0 ? informational[..plusIndex] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    public async Task<long> ResolveSeriesIdAsync(string seriesKey, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);

        var trimmed = seriesKey.Trim();
        if (long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var seriesId))
            return seriesId;

        _logger.LogDebug("Resolving TVDb series slug: {Slug}", trimmed);
        var response = await SendAuthenticatedAsync(
            HttpMethod.Get,
            $"series/slug/{Uri.EscapeDataString(trimmed)}",
            cancellationToken).ConfigureAwait(false);

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new TvDbApiException(
                    "TvDbSeriesNotFound",
                    $"TVDb series slug '{trimmed}' was not found.");
            }

            await EnsureSuccessAsync(response, "TvDbRequestFailed", $"Failed to resolve TVDb series slug '{trimmed}'.").ConfigureAwait(false);

            var payload = await DeserializeAsync(response, TvDbJsonContext.Default.TvDbSeriesResponse).ConfigureAwait(false);
            if (payload?.Data is null || payload.Data.Id <= 0)
            {
                throw new TvDbApiException(
                    "TvDbInvalidResponse",
                    $"TVDb returned an invalid series response for slug '{trimmed}'.");
            }

            return payload.Data.Id;
        }
    }

    public async Task<IReadOnlyList<TvDbEpisodeInfo>> GetSeasonEpisodesAsync(
        long seriesId,
        int season,
        string seasonType,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seriesId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(season);
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonType);

        var normalizedSeasonType = seasonType.Trim().ToLowerInvariant();
        var episodes = new List<TvDbEpisodeInfo>();
        var page = 0;

        while (true)
        {
            var relativeUri =
                $"series/{seriesId}/episodes/{Uri.EscapeDataString(normalizedSeasonType)}" +
                $"?page={page}&season={season}";

            _logger.LogDebug(
                "Fetching TVDb episodes: series={SeriesId}, season={Season}, type={SeasonType}, page={Page}",
                seriesId,
                season,
                normalizedSeasonType,
                page);

            using var response = await SendAuthenticatedAsync(HttpMethod.Get, relativeUri, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new TvDbApiException(
                    "TvDbSeriesNotFound",
                    $"TVDb series '{seriesId}' was not found.");
            }

            await EnsureSuccessAsync(
                response,
                "TvDbRequestFailed",
                $"Failed to fetch TVDb episodes for series '{seriesId}' season {season}.").ConfigureAwait(false);

            var payload = await DeserializeAsync(response, TvDbJsonContext.Default.TvDbSeriesEpisodesResponse)
                .ConfigureAwait(false);
            var pageEpisodes = payload?.Data?.Episodes;
            if (pageEpisodes is null || pageEpisodes.Count == 0)
                break;

            foreach (var episode in pageEpisodes)
            {
                if (episode.SeasonNumber is int episodeSeason && episodeSeason != season)
                    continue;

                var episodeNumber = episode.Number ?? 0;
                if (episodeNumber <= 0)
                    continue;

                var title = string.IsNullOrWhiteSpace(episode.Name)
                    ? $"Episode {episodeNumber}"
                    : episode.Name.Trim();

                episodes.Add(new TvDbEpisodeInfo(
                    episode.Id.ToString(CultureInfo.InvariantCulture),
                    season,
                    title,
                    episodeNumber));
            }

            if (string.IsNullOrWhiteSpace(payload?.Links?.Next))
                break;

            page++;
        }

        return episodes
            .OrderBy(episode => episode.EpisodeNumber)
            .ThenBy(episode => episode.Id, StringComparer.Ordinal)
            .ToList();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _httpClient.Dispose();
        _authLock.Dispose();
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpMethod method,
        string relativeUri,
        CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        using var request = CreateRequest(method, relativeUri);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        _logger.LogDebug("TVDb token rejected; re-authenticating");
        await EnsureAuthenticatedAsync(cancellationToken, forceRefresh: true).ConfigureAwait(false);

        using var retryRequest = CreateRequest(method, relativeUri);
        return await _httpClient.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        return request;
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        if (!forceRefresh && !string.IsNullOrWhiteSpace(_bearerToken))
            return;

        await _authLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && !string.IsNullOrWhiteSpace(_bearerToken))
                return;

            var apiKey = _credentials.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new TvDbApiException(
                    "TvDbApiKeyMissing",
                    $"TVDb API key is not configured. Set the {EnvironmentTvDbCredentialProvider.ApiKeyVariableName} environment variable.");
            }

            var loginPayload = new TvDbLoginRequest
            {
                ApiKey = apiKey,
                Pin = _credentials.Pin
            };

            var json = JsonSerializer.Serialize(loginPayload, TvDbJsonContext.Default.TvDbLoginRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "login") { Content = content };

            _logger.LogDebug("Authenticating with TheTVDB API");
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new TvDbApiException(
                    "TvDbAuthFailed",
                    "TVDb authentication failed. Check TVDB_API_KEY and optional TVDB_PIN.");
            }

            await EnsureSuccessAsync(response, "TvDbAuthFailed", "TVDb authentication request failed.").ConfigureAwait(false);

            var payload = await DeserializeAsync(response, TvDbJsonContext.Default.TvDbLoginResponse).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(payload?.Data?.Token))
            {
                throw new TvDbApiException(
                    "TvDbAuthFailed",
                    "TVDb authentication succeeded but no token was returned.");
            }

            _bearerToken = payload.Data.Token;
        }
        finally
        {
            _authLock.Release();
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string errorId, string message)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var detail = string.IsNullOrWhiteSpace(body)
            ? message
            : $"{message} Status={(int)response.StatusCode}. Body={Truncate(body, 300)}";

        throw new TvDbApiException(errorId, detail);
    }

    private static async Task<T?> DeserializeAsync<T>(
        HttpResponseMessage response,
        global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        try
        {
            return await JsonSerializer.DeserializeAsync(stream, typeInfo).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new TvDbApiException("TvDbInvalidResponse", "TVDb returned a response that could not be parsed.", ex);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
