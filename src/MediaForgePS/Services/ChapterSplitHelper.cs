using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Shared logic for splitting media files by chapter ranges.
/// </summary>
public static class ChapterSplitHelper
{
    /// <summary>
    /// Executes the shared chapter split workflow for a resolved input file.
    /// </summary>
    public static IReadOnlyList<string>? ExecuteSplitWorkflow(
        PSCmdlet cmdlet,
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
            cmdlet.SessionState.Path.CurrentLocation.Path);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            cmdlet.WriteError(new ErrorRecord(
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
            if (!TryGetChapters(cmdlet, resolvedInputPath, mediaFile, out chapters))
                return null;

            writeHostMessage($"Found {chapters.Length} chapters", ConsoleColor.Green);
        }
        else
            chapters = [.. preloadedChapters];

        return SplitChapterRanges(
            cmdlet,
            logger,
            executableService,
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
    public static bool TryGetChapters(PSCmdlet cmdlet, string resolvedInputPath, MediaFile? mediaFile, out MediaChapter[] chapters)
    {
        chapters = Array.Empty<MediaChapter>();
        if (mediaFile?.Chapters == null || mediaFile.Chapters.Length == 0)
        {
            cmdlet.WriteError(new ErrorRecord(
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
        PSCmdlet cmdlet,
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
        var outputFiles = new List<string>();

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

            var outputFileName = buildOutputFileName(i, range);
            var outputFile = Path.Combine(outputDirectory, outputFileName);

            if (File.Exists(outputFile))
            {
                cmdlet.WriteWarning($"Output file already exists: {outputFile}. Skipping...");
                outputFiles.Add(outputFile);
                continue;
            }

            var startChapter = chapters[chapterStart];
            var endChapter = chapters[chapterEnd];
            var startTime = (double)startChapter.StartTime;
            var endTime = (double)endChapter.EndTime;
            var duration = endTime - startTime;

            var startTimeCode = MediaConversionHelper.FormatTimeCode(startTime);
            var durationTimeCode = MediaConversionHelper.FormatTimeCode(duration);

            writeHostMessage?.Invoke(
                $"Splitting chapters {chapterStart + 1}-{chapterEnd + 1} ({startTimeCode} - {durationTimeCode}) -> {outputFileName}",
                ConsoleColor.Yellow);

            var ffmpegArgs = new List<string>
            {
                "-i", resolvedInputPath,
                "-ss", startTimeCode,
                "-t", durationTimeCode,
                "-map", "0",
                "-c", "copy",
                "-avoid_negative_ts", "make_zero",
                outputFile
            };

            logger.LogDebug("Executing ffmpeg with arguments: {Args}", string.Join(" ", ffmpegArgs));

            var result = executableService.ExecuteAsync("ffmpeg", ffmpegArgs, cancellationToken)
                .ConfigureAwait(false).GetAwaiter().GetResult();

            if (result.ExitCode != 0)
            {
                var message = $"ffmpeg failed with exit code {result.ExitCode} for output file: {outputFile}";
                if (!string.IsNullOrWhiteSpace(result.ErrorOutput))
                    message += ". " + result.ErrorOutput.Trim();
                throw new InvalidOperationException(message);
            }

            writeHostMessage?.Invoke($"Successfully created: {outputFile}", ConsoleColor.Green);
            outputFiles.Add(outputFile);
        }

        return outputFiles;
    }
}
