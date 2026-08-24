using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Providers;

/// <summary>
/// PSDrive state for the Media provider, including a bounded LRU <see cref="MediaFile"/> cache.
/// </summary>
public sealed class MediaDriveInfo : PSDriveInfo
{
    /// <summary>
    /// Default maximum number of probed media files retained per drive.
    /// </summary>
    public const int DefaultCacheCapacity = 64;

    private readonly object _cacheLock = new();
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cacheMap =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<CacheEntry> _lruList = new();
    private readonly int _cacheCapacity;

    private sealed class CacheEntry(string key, MediaFile mediaFile)
    {
        public string Key { get; } = key;

        public MediaFile MediaFile { get; set; } = mediaFile;
    }

    /// <summary>
    /// Creates a Media drive from the engine-supplied drive info.
    /// </summary>
    public MediaDriveInfo(PSDriveInfo driveInfo)
        : this(driveInfo, DefaultCacheCapacity)
    {
    }

    /// <summary>
    /// Creates a Media drive with a custom cache capacity (for testing).
    /// </summary>
    internal MediaDriveInfo(PSDriveInfo driveInfo, int cacheCapacity)
        : base(driveInfo)
    {
        if (cacheCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cacheCapacity), cacheCapacity, "Cache capacity must be positive.");

        _cacheCapacity = cacheCapacity;
    }

    /// <summary>
    /// Tries to get a cached <see cref="MediaFile"/> for <paramref name="physicalPath"/>.
    /// </summary>
    public bool TryGetCachedMediaFile(string physicalPath, out MediaFile? mediaFile)
    {
        ArgumentNullException.ThrowIfNull(physicalPath);

        lock (_cacheLock)
        {
            if (!_cacheMap.TryGetValue(physicalPath, out var node))
            {
                mediaFile = null;
                return false;
            }

            _lruList.Remove(node);
            _lruList.AddFirst(node);
            mediaFile = node.Value.MediaFile;
            return true;
        }
    }

    /// <summary>
    /// Stores <paramref name="mediaFile"/> in the drive cache.
    /// </summary>
    public void SetCachedMediaFile(string physicalPath, MediaFile mediaFile)
    {
        ArgumentNullException.ThrowIfNull(physicalPath);
        ArgumentNullException.ThrowIfNull(mediaFile);

        lock (_cacheLock)
        {
            if (_cacheMap.TryGetValue(physicalPath, out var existingNode))
            {
                existingNode.Value.MediaFile = mediaFile;
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            while (_cacheMap.Count >= _cacheCapacity)
            {
                var lruNode = _lruList.Last;
                if (lruNode is null)
                    break;

                _cacheMap.Remove(lruNode.Value.Key);
                _lruList.RemoveLast();
            }

            var node = _lruList.AddFirst(new CacheEntry(physicalPath, mediaFile));
            _cacheMap[physicalPath] = node;
        }
    }

    /// <summary>
    /// Clears the MediaFile cache for this drive.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cacheMap.Clear();
            _lruList.Clear();
        }
    }
}
