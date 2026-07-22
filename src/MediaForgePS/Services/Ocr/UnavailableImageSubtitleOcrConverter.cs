using System;
using System.Threading;

namespace Dadstart.Labs.MediaForge.Services.Ocr;

/// <summary>
/// Placeholder converter used when in-process OCR is not available on the current platform.
/// </summary>
public sealed class UnavailableImageSubtitleOcrConverter : IImageSubtitleOcrConverter
{
    /// <inheritdoc />
    public bool IsSupportedOnCurrentPlatform => false;

    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public string ExpectedTessDataDescription =>
        "Image subtitle OCR via libse/Tesseract is currently supported on Windows only.";

    /// <inheritdoc />
    public void ConvertToSrt(string inputPath, string outputSrtPath, CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException(ExpectedTessDataDescription);
    }
}
