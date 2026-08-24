using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.TvDb;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.SeriesProcessing;

public sealed class SeriesSeasonScanPhaseTests
{
    [Fact]
    public void Run_WhenNoUrlsProvided_WritesErrorAndReturnsEmpty()
    {
        var phase = CreatePhase(out _);
        var io = new FakeCmdletIO();

        var episodes = phase.Run(io, season: 1, tvDbSeriesUrl: null, tvDbSeasonUrl: null, TestContext.Current.CancellationToken);

        Assert.Empty(episodes);
        var error = Assert.Single(io.Errors);
        Assert.Equal("TvDbUrlMissing", error.FullyQualifiedErrorId);
    }

    [Fact]
    public void Run_WhenSeriesUrlIsInvalid_WritesErrorAndReturnsEmpty()
    {
        var phase = CreatePhase(out _);
        var io = new FakeCmdletIO();

        var episodes = phase.Run(
            io,
            season: 1,
            tvDbSeriesUrl: "https://example.com/not-tvdb",
            tvDbSeasonUrl: null,
            TestContext.Current.CancellationToken);

        Assert.Empty(episodes);
        var error = Assert.Single(io.Errors);
        Assert.Equal("InvalidTvDbUrl", error.FullyQualifiedErrorId);
    }

    [Fact]
    public void Run_WhenSeasonUrlIsInvalid_WritesErrorAndReturnsEmpty()
    {
        var phase = CreatePhase(out _);
        var io = new FakeCmdletIO();

        var episodes = phase.Run(
            io,
            season: 1,
            tvDbSeriesUrl: null,
            tvDbSeasonUrl: "https://thetvdb.com/series/show/invalid",
            TestContext.Current.CancellationToken);

        Assert.Empty(episodes);
        var error = Assert.Single(io.Errors);
        Assert.Equal("InvalidTvDbSeasonUrl", error.FullyQualifiedErrorId);
    }

    [Fact]
    public void Run_WhenSeriesUrlIsValid_ReturnsEpisodesFromClient()
    {
        var expected = new List<TvDbEpisodeInfo> { new("42", 1, "Pilot", 1) };
        var tvDbMock = new Mock<ITvDbClient>();
        tvDbMock.Setup(client => client.ResolveSeriesIdAsync("breaking-bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(81189L);
        tvDbMock.Setup(client => client.GetSeasonEpisodesAsync(81189L, 1, "official", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var phase = CreatePhase(tvDbMock);
        var io = new FakeCmdletIO();

        var episodes = phase.Run(
            io,
            season: 1,
            tvDbSeriesUrl: "https://thetvdb.com/series/breaking-bad",
            tvDbSeasonUrl: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, episodes);
        Assert.Empty(io.Errors);
    }

    [Fact]
    public void Run_WhenSeasonUrlIsValid_UsesSeasonFromUrl()
    {
        var tvDbMock = new Mock<ITvDbClient>();
        tvDbMock.Setup(client => client.ResolveSeriesIdAsync("breaking-bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(81189L);
        tvDbMock.Setup(client => client.GetSeasonEpisodesAsync(81189L, 2, "official", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TvDbEpisodeInfo("1", 2, "Episode", 1)]);
        var phase = CreatePhase(tvDbMock);
        var io = new FakeCmdletIO();

        var episodes = phase.Run(
            io,
            season: 1,
            tvDbSeriesUrl: null,
            tvDbSeasonUrl: "https://thetvdb.com/series/breaking-bad/seasons/official/2",
            TestContext.Current.CancellationToken);

        Assert.Single(episodes);
        tvDbMock.Verify(client => client.GetSeasonEpisodesAsync(81189L, 2, "official", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Run_WhenTvDbApiExceptionThrown_WritesMappedError()
    {
        var tvDbMock = new Mock<ITvDbClient>();
        tvDbMock.Setup(client => client.ResolveSeriesIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TvDbApiException("TvDbSeriesNotFound", "Series not found"));
        var phase = CreatePhase(tvDbMock);
        var io = new FakeCmdletIO();

        var episodes = phase.Run(
            io,
            season: 1,
            tvDbSeriesUrl: "https://thetvdb.com/series/missing-show",
            tvDbSeasonUrl: null,
            TestContext.Current.CancellationToken);

        Assert.Empty(episodes);
        var error = Assert.Single(io.Errors);
        Assert.Equal("TvDbSeriesNotFound", error.FullyQualifiedErrorId);
    }

    [Fact]
    public void Run_WhenHttpRequestExceptionThrown_WritesConnectionError()
    {
        var tvDbMock = new Mock<ITvDbClient>();
        tvDbMock.Setup(client => client.ResolveSeriesIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));
        var phase = CreatePhase(tvDbMock);
        var io = new FakeCmdletIO();

        var episodes = phase.Run(
            io,
            season: 1,
            tvDbSeriesUrl: "https://thetvdb.com/series/show",
            tvDbSeasonUrl: null,
            TestContext.Current.CancellationToken);

        Assert.Empty(episodes);
        var error = Assert.Single(io.Errors);
        Assert.Equal("TvDbRequestFailed", error.FullyQualifiedErrorId);
    }

    [Fact]
    public void Run_WhenCancelled_ThrowsOperationCanceledException()
    {
        var phase = CreatePhase(out _);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => phase.Run(
                new FakeCmdletIO(),
                season: 1,
                tvDbSeriesUrl: "https://thetvdb.com/series/show",
                tvDbSeasonUrl: null,
                cts.Token));
    }

    private static SeriesSeasonScanPhase CreatePhase(out Mock<ITvDbClient> tvDbMock)
    {
        tvDbMock = new Mock<ITvDbClient>();
        return CreatePhase(tvDbMock);
    }

    private static SeriesSeasonScanPhase CreatePhase(Mock<ITvDbClient> tvDbMock) =>
        new(tvDbMock.Object, NullLogger.Instance);
}
