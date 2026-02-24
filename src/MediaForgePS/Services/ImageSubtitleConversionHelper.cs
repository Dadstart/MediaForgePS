using System;
using System.IO;
using System.Threading;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Converts image-based subtitle files (SUP, SUB) to SRT using Subtitle Edit with Tesseract OCR.
/// </summary>
public static class ImageSubtitleConversionHelper
{
    /// <summary>
    /// Converts a single image subtitle file to SRT. Runs Subtitle Edit; moves the default output to outputSrtPath if different.
    /// Throws on failure.
    /// </summary>
    public static void ConvertToSrt(
        IExecutableService executableService,
        string subtitleEditPath,
        string inputPath,
        string outputSrtPath)
    {
        var args = new[] { "/convert", inputPath, "srt", "/ocrengine:tesseract" };
        var result = executableService.ExecuteAsync(subtitleEditPath, args, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Subtitle Edit failed with exit code {result.ExitCode}. {result.ErrorOutput}");
        var defaultSrt = Path.ChangeExtension(inputPath, "srt") ?? inputPath + ".srt";
        if (!string.Equals(defaultSrt, outputSrtPath, StringComparison.OrdinalIgnoreCase) && File.Exists(defaultSrt))
        {
            var dir = Path.GetDirectoryName(outputSrtPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.Move(defaultSrt, outputSrtPath, overwrite: true);
        }
    }
}
