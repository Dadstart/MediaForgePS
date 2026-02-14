namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Episode information from TVDb, used when passing episode metadata to Invoke-VideoCopy.
/// </summary>
public record TvDbEpisodeInfo(string Id, int SeasonNumber, string Title, int EpisodeNumber);
