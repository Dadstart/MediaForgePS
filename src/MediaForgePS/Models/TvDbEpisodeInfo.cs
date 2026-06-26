namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Episode metadata from TVDb, used for episode-based file naming.
/// </summary>
/// <param name="Id">TVDb episode ID (included in Plex-style output file names).</param>
/// <param name="SeasonNumber">Season number (1-based).</param>
/// <param name="Title">Episode title from TVDb.</param>
/// <param name="EpisodeNumber">Episode number within the season (1-based).</param>
public record TvDbEpisodeInfo(string Id, int SeasonNumber, string Title, int EpisodeNumber);
