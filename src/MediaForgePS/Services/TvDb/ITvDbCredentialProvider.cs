namespace Dadstart.Labs.MediaForge.Services.TvDb;

/// <summary>
/// Supplies TheTVDB API credentials without embedding them in source.
/// </summary>
public interface ITvDbCredentialProvider
{
    /// <summary>
    /// TheTVDB API key (for example from the <c>TVDB_API_KEY</c> environment variable).
    /// </summary>
    string? ApiKey { get; }

    /// <summary>
    /// Optional subscriber PIN for user-supported API keys (for example <c>TVDB_PIN</c>).
    /// </summary>
    string? Pin { get; }
}
