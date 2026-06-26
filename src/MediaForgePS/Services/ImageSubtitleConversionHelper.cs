using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Converts image-based subtitle files (SUP, SUB) to SRT using Subtitle Edit with Tesseract OCR.
/// </summary>
public static class ImageSubtitleConversionHelper
{
    /// <summary>
    /// Deletes image-based subtitle source files after a successful OCR conversion.
    /// Removes the input file and, for VobSub pairs, the companion .idx or .sub file.
    /// </summary>
    public static void DeleteImageSubtitleSourceFiles(string inputPath, ILogger? logger = null)
    {
        var extension = Path.GetExtension(inputPath);
        if (extension.Equals(".sup", StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(inputPath, logger);
            return;
        }

        if (extension.Equals(".sub", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".idx", StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(inputPath, logger);
            var companionExtension = extension.Equals(".sub", StringComparison.OrdinalIgnoreCase) ? ".idx" : ".sub";
            var companionPath = Path.ChangeExtension(inputPath, companionExtension);
            if (!string.IsNullOrEmpty(companionPath))
                TryDeleteFile(companionPath, logger);
        }
    }

    /// <summary>
    /// Converts a single image subtitle file to SRT. Runs Subtitle Edit; moves the default output to outputSrtPath if different.
    /// Deletes the source image subtitle file(s) when conversion succeeds. Throws on failure.
    /// </summary>
    public static void ConvertToSrt(
        IExecutableService executableService,
        string subtitleEditPath,
        string inputPath,
        string outputSrtPath,
        ILogger? logger = null)
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

        if (!File.Exists(outputSrtPath))
            throw new InvalidOperationException($"Subtitle Edit reported success but SRT output was not found: {outputSrtPath}");

        DeleteImageSubtitleSourceFiles(inputPath, logger);
    }

    private static void TryDeleteFile(string path, ILogger? logger)
    {
        if (!File.Exists(path))
            return;

        File.Delete(path);
        logger?.LogDebug("Deleted image subtitle source file after OCR: {Path}", path);
    }

    /// <summary>
    /// Converts image subtitle paths to SRT in parallel with throttling, progress reporting, and error collection.
    /// </summary>
    /// <param name="cmdlet">Cmdlet for progress and stream access.</param>
    /// <param name="executableService">Service used to run Subtitle Edit.</param>
    /// <param name="logger">Logger for debug/error.</param>
    /// <param name="subtitleEditPath">Path to Subtitle Edit executable.</param>
    /// <param name="imagePaths">Paths to .sup or .sub files.</param>
    /// <param name="throttleLimit">Maximum concurrent conversions.</param>
    /// <param name="writeError">Callback to write error records for failed conversions.</param>
    /// <returns>Paths of successfully converted SRT files.</returns>
    public static IReadOnlyList<string> ConvertImagePathsToSrtParallel(
        PSCmdlet cmdlet,
        IExecutableService executableService,
        ILogger logger,
        string subtitleEditPath,
        IReadOnlyList<string> imagePaths,
        int throttleLimit,
        Action<ErrorRecord> writeError)
    {
        var convertedSrtPaths = new ConcurrentBag<string>();
        var errors = new ConcurrentBag<(string InputPath, Exception Exception)>();
        var completedCount = 0;
        var totalConvert = imagePaths.Count;
        var maxParallel = Math.Max(1, throttleLimit);
        using var throttle = new SemaphoreSlim(maxParallel, maxParallel);

        MediaConversionHelper.WriteMainProgress(
            cmdlet,
            "Converting image subtitles to SRT",
            $"Converting {totalConvert} image subtitle file(s) to SRT...",
            0,
            recordType: ProgressRecordType.Processing);

        var tasks = new Task[imagePaths.Count];
        for (var i = 0; i < imagePaths.Count; i++)
        {
            var inputPath = imagePaths[i];
            tasks[i] = Task.Run(() =>
            {
                throttle.Wait();
                try
                {
                    var srtPath = Path.ChangeExtension(inputPath, "srt") ?? inputPath + ".srt";
                    try
                    {
                        ConvertToSrt(executableService, subtitleEditPath, inputPath, srtPath, logger);
                        logger.LogDebug("Converted image subtitles to SRT: {Path}", srtPath);
                        convertedSrtPaths.Add(srtPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to convert image subtitles to SRT: {Path}", inputPath);
                        errors.Add((inputPath, ex));
                    }
                    finally
                    {
                        Interlocked.Increment(ref completedCount);
                    }
                }
                finally
                {
                    throttle.Release();
                }
            });
        }

        while (Volatile.Read(ref completedCount) < totalConvert)
        {
            var current = Volatile.Read(ref completedCount);
            var percent = totalConvert > 0 ? (int)((current * 100.0) / totalConvert) : 0;
            MediaConversionHelper.WriteMainProgress(
                cmdlet,
                "Converting image subtitles to SRT",
                $"Converted {current} of {totalConvert} image subtitle file(s) to SRT...",
                percent,
                recordType: ProgressRecordType.Processing);
            Thread.Sleep(200);
        }

        Task.WaitAll(tasks);

        MediaConversionHelper.WriteMainProgress(
            cmdlet,
            "Converting image subtitles to SRT",
            $"Converted {totalConvert} of {totalConvert} image subtitle file(s) to SRT...",
            100,
            recordType: ProgressRecordType.Processing);

        foreach (var error in errors)
            writeError(new ErrorRecord(error.Exception, "ConvertImageSubtitlesToSrtFailed", ErrorCategory.OperationStopped, error.InputPath));

        MediaConversionHelper.WriteProgressCompleted(cmdlet, "Converting image subtitles to SRT", "Current file");
        return convertedSrtPaths.ToList();
    }
}
