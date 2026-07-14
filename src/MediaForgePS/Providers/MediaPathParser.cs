using System.IO;

namespace Dadstart.Labs.MediaForge.Providers;

/// <summary>
/// Parses Media PSProvider paths into filesystem + virtual media nodes.
/// </summary>
public static class MediaPathParser
{
    public const string FormatNode = "format";
    public const string ChaptersNode = "chapters";
    public const string StreamsNode = "streams";
    public const string AllStreamsNode = "all";

    private static readonly HashSet<string> _mediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".flv", ".webm",
        ".ts", ".m2ts", ".mts", ".mpg", ".mpeg", ".vob", ".ogv", ".3gp",
    };

    private static readonly HashSet<string> _streamTypeNodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video", "audio", "subtitle", "data", "attachment", AllStreamsNode,
    };

    /// <summary>
    /// Returns whether <paramref name="path"/> has a known media file extension.
    /// </summary>
    public static bool IsMediaFilePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _mediaExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>
    /// Returns whether <paramref name="name"/> is a known stream type folder name.
    /// </summary>
    public static bool IsStreamTypeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _streamTypeNodes.Contains(name);
    }

    /// <summary>
    /// Normalizes a provider path to use '/' separators without a leading slash.
    /// </summary>
    public static string NormalizeProviderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var normalized = path.Replace('\\', '/').Trim('/');
        while (normalized.Contains("//", StringComparison.Ordinal))
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

        return normalized;
    }

    /// <summary>
    /// Splits a normalized provider path into segments.
    /// </summary>
    public static string[] SplitSegments(string? path)
    {
        var normalized = NormalizeProviderPath(path);
        if (normalized.Length == 0)
            return [];

        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Joins provider path segments with '/'.
    /// </summary>
    public static string JoinSegments(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return string.Join('/', segments.Where(static s => !string.IsNullOrEmpty(s)));
    }

    /// <summary>
    /// Parses a provider path relative to a drive root into a <see cref="MediaPathInfo"/>.
    /// </summary>
    /// <param name="driveRoot">Absolute filesystem root of the PSDrive (file or directory).</param>
    /// <param name="providerPath">Path relative to the drive root.</param>
    /// <param name="fileExists">File existence check.</param>
    /// <param name="directoryExists">Directory existence check.</param>
    /// <returns>Parsed path info, or null when the path cannot be resolved.</returns>
    public static MediaPathInfo? TryParse(
        string driveRoot,
        string? providerPath,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(driveRoot);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryExists);

        var root = Path.GetFullPath(driveRoot);
        var normalized = NormalizeProviderPath(providerPath);
        var segments = SplitSegments(normalized);

        if (fileExists(root) && IsMediaFilePath(root))
            return TryParseUnderMediaFile(root, normalized, segments, startIndex: 0);

        if (!directoryExists(root))
            return null;

        if (segments.Length == 0)
        {
            return new MediaPathInfo(
                ProviderPath: string.Empty,
                PhysicalPath: root,
                Kind: MediaPathKind.FileSystemDirectory);
        }

        // Longest existing filesystem prefix under the directory root.
        for (var take = segments.Length; take >= 1; take--)
        {
            var relativeFs = Path.Combine(segments.AsSpan(0, take).ToArray());
            var physical = Path.GetFullPath(Path.Combine(root, relativeFs));
            if (!IsPathUnderRoot(physical, root))
                continue;

            var providerPrefix = JoinSegments(segments.AsSpan(0, take).ToArray());
            var remaining = segments.AsSpan(take).ToArray();

            if (directoryExists(physical))
            {
                if (remaining.Length > 0)
                    continue;

                return new MediaPathInfo(
                    ProviderPath: providerPrefix,
                    PhysicalPath: physical,
                    Kind: MediaPathKind.FileSystemDirectory);
            }

            if (!fileExists(physical))
                continue;

            if (IsMediaFilePath(physical))
                return TryParseUnderMediaFile(physical, JoinSegments([providerPrefix, .. remaining]), remaining, startIndex: 0, providerPrefix);

            if (remaining.Length > 0)
                return null;

            return new MediaPathInfo(
                ProviderPath: providerPrefix,
                PhysicalPath: physical,
                Kind: MediaPathKind.FileSystemFile);
        }

        return null;
    }

    private static bool IsPathUnderRoot(string physicalPath, string root)
    {
        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var physicalFull = Path.GetFullPath(physicalPath);
        if (string.Equals(physicalFull, rootFull, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = rootFull + Path.DirectorySeparatorChar;
        return physicalFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static MediaPathInfo? TryParseUnderMediaFile(
        string mediaFilePath,
        string fullProviderPath,
        string[] virtualSegments,
        int startIndex,
        string? mediaProviderPrefix = null)
    {
        var mediaNodePath = mediaProviderPrefix ?? string.Empty;
        if (virtualSegments.Length == startIndex)
        {
            return new MediaPathInfo(
                ProviderPath: NormalizeProviderPath(fullProviderPath.Length == 0 ? mediaNodePath : fullProviderPath),
                PhysicalPath: mediaFilePath,
                Kind: MediaPathKind.MediaFile);
        }

        var node = virtualSegments[startIndex];
        if (node.Equals(FormatNode, StringComparison.OrdinalIgnoreCase))
        {
            if (virtualSegments.Length != startIndex + 1)
                return null;

            return new MediaPathInfo(
                ProviderPath: NormalizeProviderPath(fullProviderPath),
                PhysicalPath: mediaFilePath,
                Kind: MediaPathKind.Format);
        }

        if (node.Equals(ChaptersNode, StringComparison.OrdinalIgnoreCase))
        {
            if (virtualSegments.Length == startIndex + 1)
            {
                return new MediaPathInfo(
                    ProviderPath: NormalizeProviderPath(fullProviderPath),
                    PhysicalPath: mediaFilePath,
                    Kind: MediaPathKind.Chapters);
            }

            if (virtualSegments.Length == startIndex + 2
                && int.TryParse(virtualSegments[startIndex + 1], out var chapterIndex)
                && chapterIndex >= 0)
            {
                return new MediaPathInfo(
                    ProviderPath: NormalizeProviderPath(fullProviderPath),
                    PhysicalPath: mediaFilePath,
                    Kind: MediaPathKind.Chapter,
                    Index: chapterIndex);
            }

            return null;
        }

        if (node.Equals(StreamsNode, StringComparison.OrdinalIgnoreCase))
        {
            if (virtualSegments.Length == startIndex + 1)
            {
                return new MediaPathInfo(
                    ProviderPath: NormalizeProviderPath(fullProviderPath),
                    PhysicalPath: mediaFilePath,
                    Kind: MediaPathKind.Streams);
            }

            if (virtualSegments.Length >= startIndex + 2
                && IsStreamTypeName(virtualSegments[startIndex + 1]))
            {
                var streamType = virtualSegments[startIndex + 1].ToLowerInvariant();
                if (virtualSegments.Length == startIndex + 2)
                {
                    return new MediaPathInfo(
                        ProviderPath: NormalizeProviderPath(fullProviderPath),
                        PhysicalPath: mediaFilePath,
                        Kind: MediaPathKind.StreamType,
                        StreamType: streamType);
                }

                if (virtualSegments.Length == startIndex + 3
                    && int.TryParse(virtualSegments[startIndex + 2], out var streamIndex)
                    && streamIndex >= 0)
                {
                    return new MediaPathInfo(
                        ProviderPath: NormalizeProviderPath(fullProviderPath),
                        PhysicalPath: mediaFilePath,
                        Kind: MediaPathKind.Stream,
                        StreamType: streamType,
                        Index: streamIndex);
                }
            }

            return null;
        }

        return null;
    }
}
