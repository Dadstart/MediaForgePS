using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services.TvDb;

/// <summary>
/// Client for TheTVDB API v4.
/// </summary>
public interface ITvDbClient
{
    /// <summary>
    /// Resolves a series slug or numeric ID to a numeric series ID.
    /// </summary>
    Task<long> ResolveSeriesIdAsync(string seriesKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns episodes for a series season from the given season type order.
    /// </summary>
    Task<IReadOnlyList<TvDbEpisodeInfo>> GetSeasonEpisodesAsync(
        long seriesId,
        int season,
        string seasonType,
        CancellationToken cancellationToken = default);
}
