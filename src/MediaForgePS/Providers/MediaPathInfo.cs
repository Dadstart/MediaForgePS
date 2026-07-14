namespace Dadstart.Labs.MediaForge.Providers;

/// <summary>
/// Resolved Media PSProvider path information.
/// </summary>
/// <param name="ProviderPath">Normalized provider-relative path using '/' separators.</param>
/// <param name="PhysicalPath">Absolute filesystem path for the nearest file or directory.</param>
/// <param name="Kind">Path classification.</param>
/// <param name="StreamType">Stream type folder name when applicable (video, audio, subtitle, data, attachment, all).</param>
/// <param name="Index">Chapter order index or stream index when applicable.</param>
public sealed record MediaPathInfo(
    string ProviderPath,
    string PhysicalPath,
    MediaPathKind Kind,
    string? StreamType = null,
    int? Index = null);
