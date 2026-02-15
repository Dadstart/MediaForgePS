namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Specifies a range of chapters to extract from a media file (1-based indices).
/// </summary>
/// <param name="Start">Starting chapter index (1-based).</param>
/// <param name="End">Ending chapter index (1-based, inclusive).</param>
/// <param name="OutputName">Optional name for the output file without extension.</param>
public record ChapterRange(int Start, int End, string? OutputName = null);
