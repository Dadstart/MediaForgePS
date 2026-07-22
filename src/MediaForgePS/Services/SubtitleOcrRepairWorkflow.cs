using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Shared OCR conversion and SRT repair workflow for subtitle cmdlets.
/// </summary>
/// <remarks>
/// OCR runs on <paramref name="imagePaths"/> when <paramref name="performOcr"/> is true.
/// Repair runs only on OCR-produced SRT paths, not pre-existing SRT in <paramref name="srtPaths"/>.
/// </remarks>
public static class SubtitleOcrRepairWorkflow
{
    /// <summary>
    /// Result of <see cref="Run"/> when the workflow completes (or has nothing to do).
    /// </summary>
    /// <param name="AllSrtPaths">Pre-existing plus OCR-produced SRT paths.</param>
    /// <param name="ConvertedSrtPaths">SRT paths produced by OCR in this run.</param>
    public sealed record Result(
        IReadOnlyList<string> AllSrtPaths,
        IReadOnlyList<string> ConvertedSrtPaths);

    /// <summary>
    /// Runs optional OCR conversion and optional repair of OCR-produced SRT files.
    /// Returns null when workflow cannot continue (for example, Tesseract language data missing when OCR is required).
    /// </summary>
    public static Result? Run(
        ICmdletIO io,
        ILogger logger,
        IImageSubtitleOcrConverter ocrConverter,
        IPathResolver pathResolver,
        IReadOnlyList<string> imagePaths,
        IReadOnlyList<string> srtPaths,
        bool performOcr,
        int throttleLimit,
        bool shouldRepair,
        string? backupPath,
        CancellationToken cancellationToken = default,
        bool keepSource = false)
    {
        IReadOnlyList<string> convertedSrtPaths = Array.Empty<string>();
        if (performOcr && imagePaths.Count > 0)
        {
            if (!ocrConverter.IsAvailable)
            {
                io.WriteError(new ErrorRecord(
                    new FileNotFoundException($"Tesseract language data not found (required to convert {imagePaths.Count} image subtitle(s)). {ocrConverter.ExpectedTessDataDescription}"),
                    "TesseractDataNotFound",
                    ErrorCategory.ObjectNotFound,
                    null));
                return null;
            }

            convertedSrtPaths = ImageSubtitleConversionHelper.ConvertImagePathsToSrtParallel(
                io,
                ocrConverter,
                logger,
                imagePaths,
                Math.Max(1, throttleLimit),
                io.WriteError,
                keepSource,
                cancellationToken);
        }

        var allSrtPaths = srtPaths.Concat(convertedSrtPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (convertedSrtPaths.Count > 0)
            SrtRepairHelper.RunRepairLoop(io, logger, pathResolver, convertedSrtPaths, shouldRepair, backupPath);

        return new Result(allSrtPaths, convertedSrtPaths);
    }
}
