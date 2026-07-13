using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Shared helpers for collecting and expanding subtitle input paths.
/// </summary>
public static class SubtitlePathResolutionHelper
{
    /// <summary>
    /// Collects non-empty, trimmed input paths.
    /// </summary>
    public static void CollectInputPaths(IEnumerable<string>? inputPaths, ICollection<string> destination)
    {
        if (inputPaths == null)
            return;

        foreach (var path in inputPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
                destination.Add(path.Trim());
        }
    }

    /// <summary>
    /// Resolves file or directory paths for subtitle-processing cmdlets.
    /// </summary>
    public static IReadOnlyList<(string ResolvedPath, bool IsDirectory)> ResolveFileOrDirectoryPaths(
        ICmdletPathContext paths,
        IReadOnlyList<string> inputPaths,
        ILogger logger,
        Action<ErrorRecord> writeError)
    {
        return PathResolver.ResolveFileOrDirectoryPaths(paths, inputPaths, logger, writeError);
    }

    /// <summary>
    /// Expands resolved path pairs into matching file paths.
    /// </summary>
    public static IReadOnlyList<string> EnumerateMatchingPaths(
        IReadOnlyList<(string ResolvedPath, bool IsDirectory)> resolvedPairs,
        SearchOption searchOption,
        string searchPattern,
        Func<string, bool> includePath)
    {
        var matchedPaths = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (resolvedPath, isDirectory) in resolvedPairs)
        {
            if (isDirectory)
            {
                foreach (var filePath in Directory.EnumerateFiles(resolvedPath, searchPattern, searchOption))
                {
                    if (!includePath(filePath) || !seenPaths.Add(filePath))
                        continue;
                    matchedPaths.Add(filePath);
                }
            }
            else
            {
                if (!includePath(resolvedPath) || !seenPaths.Add(resolvedPath))
                    continue;
                matchedPaths.Add(resolvedPath);
            }
        }

        return matchedPaths;
    }
}
