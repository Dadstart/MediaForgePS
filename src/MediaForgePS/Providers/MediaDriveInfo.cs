using System.Collections.Concurrent;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Providers;

/// <summary>
/// PSDrive state for the Media provider, including a per-drive MediaFile cache.
/// </summary>
public sealed class MediaDriveInfo : PSDriveInfo
{
    private readonly ConcurrentDictionary<string, MediaFile> _mediaFileCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a Media drive from the engine-supplied drive info.
    /// </summary>
    public MediaDriveInfo(PSDriveInfo driveInfo)
        : base(driveInfo)
    {
    }

    /// <summary>
    /// Tries to get a cached <see cref="MediaFile"/> for <paramref name="physicalPath"/>.
    /// </summary>
    public bool TryGetCachedMediaFile(string physicalPath, out MediaFile? mediaFile) =>
        _mediaFileCache.TryGetValue(physicalPath, out mediaFile);

    /// <summary>
    /// Stores <paramref name="mediaFile"/> in the drive cache.
    /// </summary>
    public void SetCachedMediaFile(string physicalPath, MediaFile mediaFile)
    {
        ArgumentNullException.ThrowIfNull(physicalPath);
        ArgumentNullException.ThrowIfNull(mediaFile);
        _mediaFileCache[physicalPath] = mediaFile;
    }

    /// <summary>
    /// Clears the MediaFile cache for this drive.
    /// </summary>
    public void ClearCache() => _mediaFileCache.Clear();
}
