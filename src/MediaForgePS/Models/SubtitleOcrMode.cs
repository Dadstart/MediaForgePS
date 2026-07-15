namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Controls when exported image-based subtitles are converted to SRT via OCR.
/// </summary>
public static class SubtitleOcrMode
{
    /// <summary>
    /// OCR image-based subtitles when the source has a single exported subtitle format and it is not SRT.
    /// When a text SRT is already present, unused image sidecars are discarded unless -KeepSource is set.
    /// </summary>
    public const string Auto = "Auto";

    /// <summary>
    /// Do not OCR image-based subtitles.
    /// </summary>
    public const string Skip = "Skip";

    /// <summary>
    /// OCR all exported image subtitle files (.sup, .sub).
    /// </summary>
    public const string Force = "Force";

    /// <summary>
    /// Default value for -Ocr parameters (<see cref="Auto"/>).
    /// </summary>
    public const string Default = Auto;

    /// <summary>
    /// Whether the OCR workflow should run (any mode except <see cref="Skip"/>).
    /// </summary>
    public static bool RequiresOcrProcessing(string? mode) =>
        !string.Equals(mode, Skip, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether OCR-converted SRT files should be repaired for the selected mode.
    /// </summary>
    public static bool ShouldRepair(string? mode, bool skipRepair) =>
        RequiresOcrProcessing(mode) && !skipRepair;
}
