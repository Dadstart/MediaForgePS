using System;
using System.Collections.Generic;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Result of subtitle extraction and/or OCR conversion for a cmdlet run.
/// </summary>
/// <param name="ExtractedCount">Number of subtitle files extracted from media.</param>
/// <param name="ConvertedCount">Number of image subtitle files successfully converted to SRT.</param>
/// <param name="ExtractedPaths">Paths of extracted subtitle sidecars (empty when no extraction ran).</param>
/// <param name="ConvertedPaths">Paths of SRT files produced by OCR (empty when no conversion ran).</param>
public sealed record SubtitleProcessingResult(
    int ExtractedCount,
    int ConvertedCount,
    IReadOnlyList<string> ExtractedPaths,
    IReadOnlyList<string> ConvertedPaths)
{
    /// <summary>
    /// Empty result with zero counts and no paths.
    /// </summary>
    public static SubtitleProcessingResult Empty { get; } =
        new(0, 0, Array.Empty<string>(), Array.Empty<string>());

    /// <summary>
    /// Builds a result from extracted and optionally converted paths.
    /// </summary>
    public static SubtitleProcessingResult Create(
        IReadOnlyList<string>? extractedPaths = null,
        IReadOnlyList<string>? convertedPaths = null)
    {
        extractedPaths ??= Array.Empty<string>();
        convertedPaths ??= Array.Empty<string>();
        return new(extractedPaths.Count, convertedPaths.Count, extractedPaths, convertedPaths);
    }
}
