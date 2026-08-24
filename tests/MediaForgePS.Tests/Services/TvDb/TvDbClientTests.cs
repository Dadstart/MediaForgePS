using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services.TvDb;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.TvDb;

public class TvDbClientTests
{
    [Fact]
    public void CreateDefaultHandler_DisablesAutomaticRedirects()
    {
        using var handler = TvDbClient.CreateDefaultHandler();

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public void Constructor_WhenHttpClientHandlerIsPassed_DisablesAutomaticRedirects()
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = true };
        using var client = CreateClient(handler, apiKey: "test-key");

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public async Task ResolveSeriesIdAsync_WhenNumeric_DoesNotCallApi()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        using var client = CreateClient(handler, apiKey: "test-key");

        var seriesId = await client.ResolveSeriesIdAsync("78804", TestContext.Current.CancellationToken);

        Assert.Equal(78804, seriesId);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ResolveSeriesIdAsync_WhenSlug_LooksUpSeries()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");

            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.EndsWith("/series/slug/breaking-bad", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("tok", request.Headers.Authorization?.Parameter);
            return JsonResponse("""{"status":"success","data":{"id":81189,"name":"Breaking Bad","slug":"breaking-bad"}}""");
        });

        using var client = CreateClient(handler, apiKey: "test-key");
        var seriesId = await client.ResolveSeriesIdAsync("breaking-bad", TestContext.Current.CancellationToken);

        Assert.Equal(81189, seriesId);
    }

    [Fact]
    public async Task GetSeasonEpisodesAsync_MapsAndSortsEpisodes()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");

            Assert.Contains("page=0", request.RequestUri!.Query, StringComparison.Ordinal);
            Assert.Contains("season=1", request.RequestUri.Query, StringComparison.Ordinal);
            Assert.Contains("/series/81189/episodes/official", request.RequestUri.AbsolutePath, StringComparison.Ordinal);
            return JsonResponse(
                """
                {
                  "status":"success",
                  "data":{
                    "episodes":[
                      {"id":2,"name":"Cat's in the Bag...","number":2,"seasonNumber":1},
                      {"id":1,"name":"Pilot","number":1,"seasonNumber":1}
                    ]
                  },
                  "links":{"next":null}
                }
                """);
        });

        using var client = CreateClient(handler, apiKey: "test-key");
        var episodes = await client.GetSeasonEpisodesAsync(81189, 1, "official", TestContext.Current.CancellationToken);

        Assert.Equal(2, episodes.Count);
        Assert.Equal(("1", "Pilot", 1), (episodes[0].Id, episodes[0].Title, episodes[0].EpisodeNumber));
        Assert.Equal(("2", "Cat's in the Bag...", 2), (episodes[1].Id, episodes[1].Title, episodes[1].EpisodeNumber));
    }

    [Fact]
    public async Task GetSeasonEpisodesAsync_WhenNextLinkNeverEnds_ThrowsAfterPageCap()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");

            return JsonResponse(
                """
                {
                  "status":"success",
                  "data":{
                    "episodes":[
                      {"id":1,"name":"Pilot","number":1,"seasonNumber":1}
                    ]
                  },
                  "links":{"next":"https://api4.thetvdb.com/v4/series/1/episodes/official?page=1"}
                }
                """);
        });

        using var client = CreateClient(handler, apiKey: "test-key");
        var ex = await Assert.ThrowsAsync<TvDbApiException>(
            () => client.GetSeasonEpisodesAsync(1, 1, "official", TestContext.Current.CancellationToken));

        Assert.Equal("TvDbPaginationLimitExceeded", ex.ErrorId);
        Assert.Equal(1 + TvDbClient.MaxSeasonEpisodePages, handler.RequestCount);
    }

    [Fact]
    public async Task GetSeasonEpisodesAsync_WhenApiKeyMissing_ThrowsTvDbApiException()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        using var client = CreateClient(handler, apiKey: null);

        var ex = await Assert.ThrowsAsync<TvDbApiException>(
            () => client.GetSeasonEpisodesAsync(1, 1, "official", TestContext.Current.CancellationToken));

        Assert.Equal("TvDbApiKeyMissing", ex.ErrorId);
    }

    [Fact]
    public async Task Login_IncludesPinWhenConfigured()
    {
        string? loginBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
            {
                loginBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");
            }

            return JsonResponse("""{"status":"success","data":{"id":1,"name":"Show","slug":"show"}}""");
        });

        using var client = CreateClient(handler, apiKey: "test-key", pin: "1234");
        await client.ResolveSeriesIdAsync("show", TestContext.Current.CancellationToken);

        Assert.NotNull(loginBody);
        Assert.Contains("\"apikey\":\"test-key\"", loginBody, StringComparison.Ordinal);
        Assert.Contains("\"pin\":\"1234\"", loginBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Requests_IncludeUserAgentWithAssemblyVersion()
    {
        var expectedVersion = typeof(TvDbClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+')[0]
            ?? typeof(TvDbClient).Assembly.GetName().Version!.ToString(3);

        string? userAgent = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");

            userAgent = request.Headers.UserAgent.ToString();
            return JsonResponse("""{"status":"success","data":{"id":1,"name":"Show","slug":"show"}}""");
        });

        using var client = CreateClient(handler, apiKey: "test-key");
        await client.ResolveSeriesIdAsync("show", TestContext.Current.CancellationToken);

        Assert.Equal($"MediaForgePS/{expectedVersion}", userAgent);
    }

    [Fact]
    public async Task SendAuthenticated_RetriesTransient5xxThenSucceeds()
    {
        var delays = new List<TimeSpan>();
        var seriesAttempts = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");

            seriesAttempts++;
            if (seriesAttempts < 3)
                return JsonResponse("""{"status":"error"}""", HttpStatusCode.ServiceUnavailable);

            return JsonResponse("""{"status":"success","data":{"id":42,"name":"Show","slug":"show"}}""");
        });

        using var client = CreateClient(handler, apiKey: "test-key", delayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var seriesId = await client.ResolveSeriesIdAsync("show", TestContext.Current.CancellationToken);

        Assert.Equal(42, seriesId);
        Assert.Equal(3, seriesAttempts);
        Assert.Equal(2, delays.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(200), delays[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(400), delays[1]);
    }

    [Fact]
    public async Task Login_RetriesTransient5xxThenSucceeds()
    {
        var loginAttempts = 0;
        var delays = new List<TimeSpan>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
            {
                loginAttempts++;
                if (loginAttempts < 2)
                    return JsonResponse("""{"status":"error"}""", HttpStatusCode.InternalServerError);

                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");
            }

            return JsonResponse("""{"status":"success","data":{"id":7,"name":"Show","slug":"show"}}""");
        });

        using var client = CreateClient(handler, apiKey: "test-key", delayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var seriesId = await client.ResolveSeriesIdAsync("show", TestContext.Current.CancellationToken);

        Assert.Equal(7, seriesId);
        Assert.Equal(2, loginAttempts);
        Assert.Equal([TimeSpan.FromMilliseconds(200)], delays);
    }

    [Fact]
    public async Task SendAuthenticated_RetriesTimeoutThenSucceeds()
    {
        var seriesAttempts = 0;
        var delays = new List<TimeSpan>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");

            seriesAttempts++;
            if (seriesAttempts == 1)
                throw new TaskCanceledException("Simulated HttpClient timeout");

            return JsonResponse("""{"status":"success","data":{"id":99,"name":"Show","slug":"show"}}""");
        });

        using var client = CreateClient(handler, apiKey: "test-key", delayAsync: (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var seriesId = await client.ResolveSeriesIdAsync("show", TestContext.Current.CancellationToken);

        Assert.Equal(99, seriesId);
        Assert.Equal(2, seriesAttempts);
        Assert.Equal([TimeSpan.FromMilliseconds(200)], delays);
    }

    [Fact]
    public async Task SendAuthenticated_WhenTransient5xxExhausted_ThrowsTvDbApiException()
    {
        var seriesAttempts = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
                return JsonResponse("""{"status":"success","data":{"token":"tok"}}""");

            seriesAttempts++;
            return JsonResponse("""{"status":"error"}""", HttpStatusCode.BadGateway);
        });

        using var client = CreateClient(handler, apiKey: "test-key", delayAsync: (_, _) => Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<TvDbApiException>(
            () => client.ResolveSeriesIdAsync("show", TestContext.Current.CancellationToken));

        Assert.Equal("TvDbRequestFailed", ex.ErrorId);
        Assert.Equal(TvDbClient.MaxTransientAttempts, seriesAttempts);
    }

    private static TvDbClient CreateClient(
        HttpMessageHandler handler,
        string? apiKey,
        string? pin = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null) =>
        new(
            new StubCredentialProvider(apiKey, pin),
            NullLogger<TvDbClient>.Instance,
            handler,
            disposeHandler: true,
            delayAsync: delayAsync);

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubCredentialProvider(string? apiKey, string? pin) : ITvDbCredentialProvider
    {
        public string? ApiKey { get; } = apiKey;
        public string? Pin { get; } = pin;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            try
            {
                return Task.FromResult(responder(request));
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}
