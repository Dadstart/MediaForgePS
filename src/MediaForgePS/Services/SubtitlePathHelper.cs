using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helpers for classifying subtitle file paths by extension.
/// </summary>
public static class SubtitlePathHelper
{
    /// <summary>
    /// Whether the extension is an image-based subtitle format (SUP or SUB).
    /// </summary>
    public static bool IsImageSubtitleExtension(string extension) =>
        extension.Equals(".sup", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".sub", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the extension is SRT.
    /// </summary>
    public static bool IsSrtExtension(string extension) =>
        extension.Equals(".srt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the file path has an image subtitle extension (.sup or .sub).
    /// </summary>
    public static bool IsImageSubtitlePath(string path) =>
        IsImageSubtitleExtension(Path.GetExtension(path));

    /// <summary>
    /// Whether the file path has an SRT extension.
    /// </summary>
    public static bool IsSrtPath(string path) =>
        IsSrtExtension(Path.GetExtension(path));

    /// <summary>
    /// Filters paths to those with image subtitle extensions (.sup, .sub).
    /// </summary>
    public static IReadOnlyList<string> GetImageSubtitlePaths(IEnumerable<string> paths) =>
        paths.Where(IsImageSubtitlePath).ToList();

    /// <summary>
    /// Filters paths to those with .srt extension.
    /// </summary>
    public static IReadOnlyList<string> GetSrtPaths(IEnumerable<string> paths) =>
        paths.Where(IsSrtPath).ToList();
}
