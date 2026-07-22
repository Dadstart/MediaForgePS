using System.Threading;

namespace Dadstart.Labs.MediaForge.Services.Ocr;

/// <summary>
/// Converts image-based subtitle files (SUP, SUB/IDX) to SubRip (SRT) via OCR.
/// </summary>
public interface IImageSubtitleOcrConverter
{
    /// <summary>
    /// Whether OCR is supported on the current OS platform.
    /// </summary>
    bool IsSupportedOnCurrentPlatform { get; }

    /// <summary>
    /// Whether the OCR runtime (Tesseract language data) is available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Human-readable description of where Tesseract language data is expected when unavailable,
    /// or why the platform is unsupported.
    /// </summary>
    string ExpectedTessDataDescription { get; }

    /// <summary>
    /// OCRs <paramref name="inputPath"/> and writes SubRip text to <paramref name="outputSrtPath"/>.
    /// </summary>
    void ConvertToSrt(string inputPath, string outputSrtPath, CancellationToken cancellationToken = default);
}
