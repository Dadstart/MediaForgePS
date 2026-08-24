using System;
using System.IO;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Result of <see cref="PathHelper.MoveFile"/>.
/// </summary>
/// <param name="SourceRemoved">Whether the source file was removed after the destination was written.</param>
/// <param name="SourceDeleteError">Error message when the source could not be removed after a successful copy.</param>
public readonly record struct FileMoveResult(bool SourceRemoved, string? SourceDeleteError);

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

    /// <summary>
    /// Returns whether two paths reside on the same volume (drive or UNC share root).
    /// </summary>
    public static bool IsSameVolume(string path1, string path2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path1);
        ArgumentException.ThrowIfNullOrWhiteSpace(path2);

        var root1 = Path.GetPathRoot(Path.GetFullPath(path1));
        var root2 = Path.GetPathRoot(Path.GetFullPath(path2));
        return !string.IsNullOrEmpty(root1)
            && !string.IsNullOrEmpty(root2)
            && string.Equals(root1, root2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Moves a file to <paramref name="destinationPath"/>, using <see cref="File.Move(string,string)"/>
    /// when source and destination share a volume; otherwise copies then deletes the source.
    /// </summary>
    /// <returns>Outcome describing whether the source was removed.</returns>
    public static FileMoveResult MoveFile(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (IsSameVolume(sourcePath, destinationPath))
        {
            File.Move(sourcePath, destinationPath);
            return new FileMoveResult(SourceRemoved: true, SourceDeleteError: null);
        }

        File.Copy(sourcePath, destinationPath);
        return DeleteSourceAfterCopy(sourcePath);
    }

    internal static FileMoveResult DeleteSourceAfterCopy(string sourcePath)
    {
        try
        {
            File.Delete(sourcePath);
            return new FileMoveResult(SourceRemoved: true, SourceDeleteError: null);
        }
        catch (Exception ex)
        {
            return new FileMoveResult(SourceRemoved: false, SourceDeleteError: ex.Message);
        }
    }
}
