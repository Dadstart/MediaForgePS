using System;
using System.IO;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helper class for path resolution and directory operations.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Returns the file name portion of a path, treating both <c>/</c> and <c>\</c> as separators
    /// so Windows-style paths display correctly on Unix hosts.
    /// </summary>
    /// <param name="path">Full or relative path.</param>
    /// <returns>The leaf file name, or <paramref name="path"/> when empty or separator-free.</returns>
    public static string GetFileName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var separatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return separatorIndex >= 0 ? path[(separatorIndex + 1)..] : path;
    }

    /// <summary>
    /// Resolves a path to an absolute path. If the path is already rooted, returns its full form;
    /// otherwise combines it with the current location and returns the full path.
    /// </summary>
    /// <param name="path">The path to resolve (relative or absolute).</param>
    /// <param name="currentLocationPath">The current working directory used for relative paths.</param>
    /// <returns>The resolved absolute path.</returns>
    public static string ResolveAbsolutePath(string path, string currentLocationPath)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return Path.GetFullPath(Path.Combine(currentLocationPath, path));
    }

    /// <summary>
    /// Resolves the output directory for a command. If outputPath is null or whitespace, returns the
    /// directory of the resolved input path; otherwise resolves outputPath (via the provided
    /// resolver or current location), ensures the directory exists, and returns it.
    /// </summary>
    /// <param name="outputPath">Optional output path (directory). Null or whitespace to use input file's directory.</param>
    /// <param name="resolvedInputPath">The already-resolved absolute path of the input file.</param>
    /// <param name="currentLocationPath">The current working directory for relative output paths.</param>
    /// <param name="tryResolveOutputPath">Delegate that attempts to resolve a full output file path (e.g. directory + dummy filename). Returns (true, resolvedPath) on success.</param>
    /// <returns>The resolved output directory path, or null if it could not be determined.</returns>
    public static string? ResolveOutputDirectory(
        string? outputPath,
        string resolvedInputPath,
        string currentLocationPath,
        Func<string, (bool success, string? resolvedPath)> tryResolveOutputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return Path.GetDirectoryName(resolvedInputPath);

        var pathToResolve = Path.Combine(outputPath.Trim(), "dummy.mkv");
        string? resolved;
        if (tryResolveOutputPath(pathToResolve) is (true, { } r))
            resolved = r;
        else
            resolved = Path.GetFullPath(Path.Combine(currentLocationPath, outputPath.Trim(), "dummy.mkv"));

        var dir = Path.GetDirectoryName(resolved);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return dir;
    }
}
