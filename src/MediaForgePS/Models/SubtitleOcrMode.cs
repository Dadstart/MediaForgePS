namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Controls when exported image-based subtitles are converted to SRT via OCR.
/// </summary>
public static class SubtitleOcrMode
{
    /// <summary>
    /// OCR image-based subtitles when the source has a single exported subtitle format and it is not SRT.
    /// </summary>
    public const string Auto = "Auto";

    /// <summary>
    /// Do not OCR image-based subtitles.
    /// </summary>
    public const string Skip = "Skip";

    /// <summary>
    /// OCR all exported .sub files.
    /// </summary>
    public const string Force = "Force";

    /// <summary>
    /// Default <see cref="Ocr"/> parameter value.
    /// </summary>
    public const string Default = Auto;

    /// <summary>
    /// Whether OCR or repair processing should run for the selected mode.
    /// </summary>
    public static bool RequiresOcrProcessing(string? mode) =>
        !string.Equals(mode, Skip, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether OCR-converted SRT files should be repaired for the selected mode.
    /// </summary>
    public static bool ShouldRepair(string? mode, bool skipRepair) =>
        RequiresOcrProcessing(mode) && !skipRepair;
}
