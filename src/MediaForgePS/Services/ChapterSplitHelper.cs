using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Shared logic for splitting media files by chapter ranges.
/// </summary>
public static class ChapterSplitHelper
{
    private const double ExistingOutputDurationToleranceSeconds = 1.0;

    /// <summary>
    /// Executes the shared chapter split workflow for a resolved input file.
    /// </summary>
    public static IReadOnlyList<string>? ExecuteSplitWorkflow(
        ICmdletIO io,
        ILogger logger,
        IMediaReaderService mediaReaderService,
        IExecutableService executableService,
        IPathResolver pathResolver,
        string resolvedInputPath,
        string? outputPath,
        IReadOnlyList<(int Start, int End, string? OutputName)> ranges,
        Func<int, (int Start, int End, string? OutputName), string> buildOutputFileName,
        Action<string, ConsoleColor?> writeHostMessage,
        IReadOnlyList<MediaChapter>? preloadedChapters = null,
        CancellationToken cancellationToken = default)
    {
        var outputDirectory = ResolveOutputDirectory(
            pathResolver,
            outputPath,
            resolvedInputPath,
            io.Paths.CurrentLocationPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            io.WriteError(new ErrorRecord(
                new InvalidOperationException("Could not resolve output directory."),
                "OutputPathResolutionFailed",
                ErrorCategory.InvalidOperation,
                outputPath));
            return null;
        }

        MediaChapter[] chapters;
        if (preloadedChapters == null)
        {
            writeHostMessage($"Getting chapter information from: {resolvedInputPath}", ConsoleColor.Cyan);
            var mediaFile = ReadMediaFile(mediaReaderService, resolvedInputPath, cancellationToken);
            if (!TryGetChapters(io, resolvedInputPath, mediaFile, out chapters))
                return null;

            writeHostMessage($"Found {chapters.Length} chapters", ConsoleColor.Green);
        }
        else
            chapters = [.. preloadedChapters];

        return SplitChapterRanges(
            io,
            logger,
            executableService,
            mediaReaderService,
            resolvedInputPath,
            outputDirectory,
            ranges,
            chapters,
            buildOutputFileName,
            writeHostMessage,
            cancellationToken);
    }

    /// <summary>
    /// Resolves the output directory for chapter splitting.
    /// </summary>
    public static string? ResolveOutputDirectory(
        IPathResolver pathResolver,
        string? outputPath,
        string resolvedInputPath,
        string currentLocationPath)
    {
        return PathHelper.ResolveOutputDirectory(
            outputPath,
            resolvedInputPath,
            currentLocationPath,
            path =>
            {
                var ok = pathResolver.TryResolveOutputPath(path, out var resolved);
                return (ok, resolved);
            });
    }

    /// <summary>
    /// Reads media metadata for chapter splitting.
    /// </summary>
    public static MediaFile? ReadMediaFile(
        IMediaReaderService mediaReaderService,
        string resolvedInputPath,
        CancellationToken cancellationToken = default)
    {
        return mediaReaderService.GetMediaFileAsync(resolvedInputPath, cancellationToken)
            .ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Validates that media metadata contains chapter information and writes a cmdlet error when missing.
    /// </summary>
    public static bool TryGetChapters(ICmdletErrorSink errors, string resolvedInputPath, MediaFile? mediaFile, out MediaChapter[] chapters)
    {
        chapters = Array.Empty<MediaChapter>();
        if (mediaFile?.Chapters == null || mediaFile.Chapters.Length == 0)
        {
            errors.WriteError(new ErrorRecord(
                new InvalidOperationException("No chapters found in video file."),
                "NoChapters",
                ErrorCategory.InvalidOperation,
                resolvedInputPath));
            return false;
        }

        chapters = mediaFile.Chapters;
        return true;
    }

    /// <summary>
    /// Splits the input file into output files for each chapter range.
    /// </summary>
    public static IReadOnlyList<string> SplitChapterRanges(
        ICmdletIO io,
        ILogger logger,
        IExecutableService executableService,
        string resolvedInputPath,
        string outputDirectory,
        IReadOnlyList<(int Start, int End, string? OutputName)> ranges,
        IReadOnlyList<MediaChapter> chapters,
        Func<int, (int Start, int End, string? OutputName), string> buildOutputFileName,
        Action<string, ConsoleColor?>? writeHostMessage = null,
        CancellationToken cancellationToken = default)
    {
        return SplitChapterRanges(
            io,
            logger,
            executableService,
            mediaReaderService: null,
            resolvedInputPath,
            outputDirectory,
            ranges,
            chapters,
            buildOutputFileName,
            writeHostMessage,
            cancellationToken);
    }

    /// <summary>
    /// Splits the input file into output files for each chapter range.
    /// </summary>
    public static IReadOnlyList<string> SplitChapterRanges(
        ICmdletIO io,
        ILogger logger,
        IExecutableService executableService,
        IMediaReaderService? mediaReaderService,
        string resolvedInputPath,
        string outputDirectory,
        IReadOnlyList<(int Start, int End, string? OutputName)> ranges,
        IReadOnlyList<MediaChapter> chapters,
        Func<int, (int Start, int End, string? OutputName), string> buildOutputFileName,
        Action<string, ConsoleColor?>? writeHostMessage = null,
        CancellationToken cancellationToken = default)
    {
        var outputFiles = new List<string>();
        var resolvedOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(resolvedOutputDirectory);

        for (var i = 0; i < ranges.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var range = ranges[i];
            var chapterStart = range.Start - 1;
            var chapterEnd = range.End - 1;

            if (chapterStart < 0 || chapterEnd < 0)
            {
                throw new ArgumentException(
                    $"Chapter indices must be positive. Range at index {i} has Start={range.Start}, End={range.End}.");
            }

            if (chapterStart >= chapters.Count || chapterEnd >= chapters.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(ranges),
                    $"Chapter range out of bounds. Available chapters: 1-{chapters.Count}. Range at index {i}: {range.Start}-{range.End}.");
            }

            if (chapterStart > chapterEnd)
            {
                throw new ArgumentException(
                    $"Start ({range.Start}) must be less than or equal to End ({range.End}) for range at index {i}.");
            }

            var rawOutputFileName = buildOutputFileName(i, range);
            string outputFile;
            try
            {
                var safeFileName = PathSafetyHelper.SanitizePathSegment(rawOutputFileName, replaceInvalidChars: false);
                outputFile = PathSafetyHelper.GetContainedFilePath(resolvedOutputDirectory, safeFileName);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(
                    $"Output file name for range at index {i} is invalid: {ex.Message}",
                    nameof(ranges),
                    ex);
            }

            var startChapter = chapters[chapterStart];
            var endChapter = chapters[chapterEnd];
            var startTime = (double)startChapter.StartTime;
            var endTime = (double)endChapter.EndTime;
            var duration = endTime - startTime;

            if (File.Exists(outputFile))
            {
                if (IsValidExistingChapterOutput(mediaReaderService, outputFile, duration, cancellationToken))
                {
                    io.WriteWarning($"Output file already exists: {outputFile}. Skipping...");
                    outputFiles.Add(outputFile);
                    continue;
                }

                io.WriteWarning($"Existing output file appears incomplete or invalid and will be regenerated: {outputFile}");
                AtomicFileHelper.TryDelete(outputFile);
            }

            var startTimeCode = MediaConversionHelper.FormatTimeCode(startTime);
            var durationTimeCode = MediaConversionHelper.FormatTimeCode(duration);
            var outputFileName = Path.GetFileName(outputFile);

            writeHostMessage?.Invoke(
                $"Splitting chapters {chapterStart + 1}-{chapterEnd + 1} ({startTimeCode} - {durationTimeCode}) -> {outputFileName}",
                ConsoleColor.Yellow);

            var tempOutputFile = AtomicFileHelper.CreateTempOutputPath(outputFile);
            var tempDirectory = Path.GetDirectoryName(tempOutputFile);
            try
            {
                var ffmpegArgs = new List<string>
                {
                    "-i", resolvedInputPath,
                    "-ss", startTimeCode,
                    "-t", durationTimeCode,
                    "-map", "0",
                    "-c", "copy",
                    "-avoid_negative_ts", "make_zero",
                    "-y",
                    tempOutputFile
                };

                logger.LogDebug("Executing ffmpeg with arguments: {Args}", string.Join(" ", ffmpegArgs));

                var result = executableService.ExecuteAsync("ffmpeg", ffmpegArgs, cancellationToken)
                    .ConfigureAwait(false).GetAwaiter().GetResult();

                result.EnsureProcessSuccess($"ffmpeg chapter split for '{outputFile}'");
                AtomicFileHelper.PromoteTempFile(tempOutputFile, outputFile);
            }
            finally
            {
                AtomicFileHelper.TryDeleteDirectory(tempDirectory);
            }

            writeHostMessage?.Invoke($"Successfully created: {outputFile}", ConsoleColor.Green);
            outputFiles.Add(outputFile);
        }

        return outputFiles;
    }

    /// <summary>
    /// Returns true when an existing chapter output is safe to skip remuxing: non-empty on disk,
    /// and (when a media reader is available) its probed duration matches the expected chapter span
    /// within <see cref="ExistingOutputDurationToleranceSeconds"/>. Corrupt or unreadable files
    /// return false so the caller can regenerate them.
    /// </summary>
    private static bool IsValidExistingChapterOutput(
        IMediaReaderService? mediaReaderService,
        string outputFile,
        double expectedDurationSeconds,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(outputFile);
        if (!info.Exists || info.Length <= 0)
            return false;

        if (mediaReaderService is null)
            return true;

        try
        {
            var media = mediaReaderService.GetMediaFileAsync(outputFile, cancellationToken)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (media is null)
                return false;

            var actualDuration = (double)media.Format.Duration;
            return Math.Abs(actualDuration - expectedDurationSeconds) <= ExistingOutputDurationToleranceSeconds;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
