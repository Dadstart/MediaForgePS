using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Validates that user-controlled path segments remain beneath an approved root.
/// </summary>
public static class PathSafetyHelper
{
    /// <summary>
    /// Both Windows and Unix separators so traversal payloads are rejected on every OS.
    /// </summary>
    private static readonly char[] _pathSeparators = ['/', '\\'];

    /// <summary>
    /// Characters that are invalid on any supported OS, plus the host platform set.
    /// Ensures sanitized names stay portable (e.g. ':' and '?' replaced on Linux/macOS too).
    /// </summary>
    private static readonly char[] _invalidFileNameChars = BuildInvalidFileNameChars();

    /// <summary>
    /// Sanitizes a single directory or file-name segment by replacing invalid characters.
    /// Rejects empty, rooted, traversal, and separator-containing values.
    /// </summary>
    public static string SanitizePathSegment(string segment, bool replaceInvalidChars = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segment);

        var trimmed = segment.Trim();
        if (trimmed is "." or "..")
            throw new ArgumentException("Path segment cannot be '.' or '..'.", nameof(segment));

        if (Path.IsPathRooted(trimmed))
            throw new ArgumentException("Path segment cannot be rooted.", nameof(segment));

        if (trimmed.IndexOfAny(_pathSeparators) >= 0 || trimmed.Contains('\0'))
            throw new ArgumentException("Path segment cannot contain directory separators.", nameof(segment));

        if (!replaceInvalidChars)
        {
            if (trimmed.IndexOfAny(_invalidFileNameChars) >= 0)
                throw new ArgumentException("Path segment contains invalid filename characters.", nameof(segment));
            return trimmed;
        }

        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            builder.Append(Array.IndexOf(_invalidFileNameChars, ch) >= 0 ? '_' : ch);

        var sanitized = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(sanitized) || sanitized is "." or "..")
            throw new ArgumentException("Path segment is empty after sanitization.", nameof(segment));

        return sanitized;
    }

    /// <summary>
    /// Combines <paramref name="rootDirectory"/> with a sanitized file-name segment and verifies containment.
    /// </summary>
    public static string GetContainedFilePath(string rootDirectory, string fileNameSegment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var safeName = SanitizePathSegment(fileNameSegment, replaceInvalidChars: false);
        return EnsurePathUnderRoot(rootDirectory, Path.Combine(rootDirectory, safeName));
    }

    /// <summary>
    /// Combines <paramref name="rootDirectory"/> with a relative path and verifies the result stays under root.
    /// </summary>
    public static string GetContainedRelativePath(string rootDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("Relative path cannot be rooted.", nameof(relativePath));

        var parts = relativePath.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            throw new ArgumentException("Relative path is empty.", nameof(relativePath));

        if (parts.Any(part => part is "." or ".."))
            throw new ArgumentException("Relative path cannot contain '.' or '..' segments.", nameof(relativePath));

        var sanitizedParts = parts.Select(part => SanitizePathSegment(part, replaceInvalidChars: false)).ToArray();
        var combined = Path.Combine(new[] { rootDirectory }.Concat(sanitizedParts).ToArray());
        return EnsurePathUnderRoot(rootDirectory, combined);
    }

    /// <summary>
    /// Ensures <paramref name="candidatePath"/> resolves beneath <paramref name="rootDirectory"/>.
    /// </summary>
    public static string EnsurePathUnderRoot(string rootDirectory, string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);

        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        var candidateFull = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(candidateFull, rootFull, comparison))
            return candidateFull;

        var prefix = rootFull + Path.DirectorySeparatorChar;
        if (!candidateFull.StartsWith(prefix, comparison))
            throw new ArgumentException($"Path '{candidatePath}' escapes the approved root '{rootDirectory}'.");

        return candidateFull;
    }

    private static char[] BuildInvalidFileNameChars()
    {
        // Portable Windows-reserved set (valid on some Unix hosts otherwise).
        char[] portableInvalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
        return Path.GetInvalidFileNameChars()
            .Concat(portableInvalid)
            .Distinct()
            .ToArray();
    }
}

