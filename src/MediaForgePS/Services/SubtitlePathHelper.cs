using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helpers for classifying subtitle file paths by extension.
/// </summary>
public static class SubtitlePathHelper
{
    private static readonly Regex _subtitleExportNamePattern = new(
        @"^(?<media>.+?)(?:\.\d+)?\.eng\.sdh$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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

    /// <summary>
    /// Derives the exported media base key for a subtitle path produced by <see cref="SubtitleExportHelper.GetOutputPath"/>.
    /// </summary>
    public static string GetMediaBaseKeyFromSubtitlePath(string subtitlePath)
    {
        var directory = Path.GetDirectoryName(subtitlePath) ?? string.Empty;
        var exportStem = Path.GetFileNameWithoutExtension(subtitlePath);
        var match = _subtitleExportNamePattern.Match(exportStem);
        if (!match.Success)
            return subtitlePath;

        return Path.Combine(directory, match.Groups["media"].Value);
    }

    /// <summary>
    /// Selects image subtitle paths to OCR based on <paramref name="ocrMode"/>.
    /// </summary>
    public static IReadOnlyList<string> SelectImagePathsForOcr(IEnumerable<string> exportedPaths, string ocrMode)
    {
        if (string.Equals(ocrMode, SubtitleOcrMode.Skip, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        var imagePaths = GetImageSubtitlePaths(exportedPaths);
        if (imagePaths.Count == 0)
            return imagePaths;

        if (string.Equals(ocrMode, SubtitleOcrMode.Force, StringComparison.OrdinalIgnoreCase))
            return imagePaths;

        var selected = new List<string>();
        foreach (var group in exportedPaths.GroupBy(GetMediaBaseKeyFromSubtitlePath, StringComparer.OrdinalIgnoreCase))
        {
            var groupPaths = group.ToList();
            if (!ShouldAutoOcrImageSubtitles(groupPaths))
                continue;

            foreach (var path in groupPaths)
            {
                if (IsImageSubtitlePath(path))
                    selected.Add(path);
            }
        }

        return selected;
    }

    /// <summary>
    /// Whether Auto mode should OCR image subtitles for one source media's exported paths.
    /// Auto runs OCR when the source has a single subtitle format and it is not SRT.
    /// </summary>
    public static bool ShouldAutoOcrImageSubtitles(IReadOnlyList<string> groupPaths)
    {
        if (groupPaths.Count == 0)
            return false;

        var extensions = groupPaths
            .Select(path => Path.GetExtension(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return extensions.Count == 1 && !IsSrtExtension(extensions[0]);
    }
}
