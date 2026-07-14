namespace Dadstart.Labs.MediaForge.Providers;

/// <summary>
/// Virtual container node exposed by the Media PSProvider (chapters, streams, stream types).
/// </summary>
/// <param name="Name">Container name.</param>
/// <param name="MediaPath">Filesystem path of the parent media file.</param>
public sealed record MediaContainerItem(string Name, string MediaPath);
