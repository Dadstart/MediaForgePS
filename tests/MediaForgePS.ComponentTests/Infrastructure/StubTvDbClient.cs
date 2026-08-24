using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.TvDb;

namespace Dadstart.Labs.MediaForge.ComponentTests.Infrastructure;

/// <summary>
/// Fixed TVDb responses for series pipeline component tests (no network or API key required).
/// </summary>
public sealed class StubTvDbClient(params TvDbEpisodeInfo[] episodes) : ITvDbClient
{
    public static readonly TvDbEpisodeInfo[] DefaultSeasonOneEpisodes =
    [
        new("1001", 1, "Pilot", 1),
        new("1002", 1, "Second", 2)
    ];

    private readonly IReadOnlyList<TvDbEpisodeInfo> _episodes = episodes;

    public Task<long> ResolveSeriesIdAsync(string seriesKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(99_999L);

    public Task<IReadOnlyList<TvDbEpisodeInfo>> GetSeasonEpisodesAsync(
        long seriesId,
        int season,
        string seasonType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_episodes);
}
