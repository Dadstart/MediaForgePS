using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Splits a video file into multiple files based on chapter ranges.
/// </summary>
/// <remarks>
/// Uses ffprobe to read chapter information and ffmpeg to split by time ranges.
/// Chapter indices in ranges are 1-based (e.g. Start=1, End=1 is the first chapter).
/// </remarks>
[Cmdlet(VerbsCommon.Split, "Chapters", DefaultParameterSetName = "ByRanges")]
[OutputType(typeof(string[]))]
public class SplitChaptersCommand : CmdletBase
{
    /// <summary>
    /// Path to the input video file.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    /// Chapter ranges. Each range has Start (1-based), End (1-based inclusive), and optional OutputName.
    /// </summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = "ByRanges")]
    [ValidateNotNull]
    public object[] ChapterRanges { get; set; } = [];

    /// <summary>
    /// When specified, splits every chapter into its own file. Mutually exclusive with -ChapterRanges.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "AllChapters")]
    public SwitchParameter AllChapters { get; set; }

    /// <summary>
    /// Directory where output files are saved. Defaults to the input file's directory.
    /// </summary>
    [Parameter(Mandatory = false)]
    public string? OutputPath { get; set; }

    private readonly List<string> _inputFiles = [];
    private IMediaReaderService? _mediaReaderService;
    private IExecutableService? _executableService;
    private IPathResolver? _pathResolver;

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();
    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    protected override void Process()
    {
        if (!string.IsNullOrWhiteSpace(InputFile))
            _inputFiles.Add(InputFile);
    }

    protected override void End()
    {
        if (_inputFiles.Count == 0)
        {
            WriteWarning("No input file(s) provided. Use the InputFile parameter to specify the input file(s).");
            return;
        }

        if (ParameterSetName == "AllChapters")
        {
            foreach (var inputPath in _inputFiles)
            {
                try
                {
                    SplitAllChaptersForFile(inputPath);
                }
                catch (Exception ex)
                {
                    WriteError(new ErrorRecord(ex, "SplitChaptersFailed", ErrorCategory.OperationStopped, inputPath));
                }
            }

            return;
        }

        var normalizedRanges = ChapterRangeHelper.NormalizeChapterRanges(ChapterRanges);
        if (normalizedRanges.Count == 0)
        {
            WriteError(new ErrorRecord(
                new ArgumentException("At least one valid chapter range with Start and End is required."),
                "InvalidChapterRanges",
                ErrorCategory.InvalidArgument,
                ChapterRanges));
            return;
        }

        foreach (var inputPath in _inputFiles)
        {
            try
            {
                SplitChaptersForFile(inputPath, normalizedRanges);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "SplitChaptersFailed", ErrorCategory.OperationStopped, inputPath));
            }
        }
    }

    private void SplitAllChaptersForFile(string inputPath)
    {
        if (!TryResolveInputPath(PathResolver, inputPath, out var resolvedInputPath))
            return;

        WriteHostMessage($"Getting chapter information from: {resolvedInputPath}", ConsoleColor.Cyan);

        var mediaFile = MediaReaderService.GetMediaFileAsync(resolvedInputPath, CancellationToken.None)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        if (mediaFile?.Chapters == null || mediaFile.Chapters.Length == 0)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("No chapters found in video file."),
                "NoChapters",
                ErrorCategory.InvalidOperation,
                resolvedInputPath));
            return;
        }

        var chapterCount = mediaFile.Chapters.Length;
        var ranges = new List<(int Start, int End, string? OutputName)>(chapterCount);
        for (var i = 1; i <= chapterCount; i++)
            ranges.Add((i, i, null));

        SplitChaptersForFile(resolvedInputPath, ranges, mediaFile);
    }

    private void SplitChaptersForFile(string inputPath, List<(int Start, int End, string? OutputName)> ranges, MediaFile? preFetchedMediaFile = null)
    {
        if (!TryResolveInputPath(PathResolver, inputPath, out var resolvedInputPath))
            return;

        var outputDir = ResolveOutputDirectory(resolvedInputPath);
        if (string.IsNullOrEmpty(outputDir))
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("Could not resolve output directory."),
                "OutputPathResolutionFailed",
                ErrorCategory.InvalidOperation,
                OutputPath));
            return;
        }

        MediaFile mediaFile;
        if (preFetchedMediaFile != null)
        {
            mediaFile = preFetchedMediaFile;
        }
        else
        {
            WriteHostMessage($"Getting chapter information from: {resolvedInputPath}", ConsoleColor.Cyan);

            mediaFile = MediaReaderService.GetMediaFileAsync(resolvedInputPath, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Could not read media file.");
        }

        if (mediaFile.Chapters == null || mediaFile.Chapters.Length == 0)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("No chapters found in video file."),
                "NoChapters",
                ErrorCategory.InvalidOperation,
                resolvedInputPath));
            return;
        }

        var chapters = mediaFile.Chapters;
        WriteHostMessage($"Found {chapters.Length} chapters", ConsoleColor.Green);

        var inputExtension = Path.GetExtension(resolvedInputPath);
        if (string.IsNullOrWhiteSpace(inputExtension))
            inputExtension = ".mkv";

        var baseName = Path.GetFileNameWithoutExtension(resolvedInputPath);
        var outputFiles = new List<string>();

        for (var i = 0; i < ranges.Count; i++)
        {
            var (startOneBased, endOneBased, outputName) = ranges[i];
            var chapterStart = startOneBased - 1;
            var chapterEnd = endOneBased - 1;

            if (chapterStart < 0 || chapterEnd < 0)
            {
                throw new ArgumentException(
                    $"Chapter indices must be positive. Range at index {i} has Start={startOneBased}, End={endOneBased}.");
            }

            if (chapterStart >= chapters.Length || chapterEnd >= chapters.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(ChapterRanges),
                    $"Chapter range out of bounds. Available chapters: 1-{chapters.Length}. Range at index {i}: {startOneBased}-{endOneBased}.");
            }

            if (chapterStart > chapterEnd)
            {
                throw new ArgumentException(
                    $"Start ({startOneBased}) must be less than or equal to End ({endOneBased}) for range at index {i}.");
            }

            var outputFileName = !string.IsNullOrWhiteSpace(outputName)
                ? outputName + inputExtension
                : $"{baseName}.split-{(i + 1):D2}{inputExtension}";
            var outputFile = Path.Combine(outputDir, outputFileName);

            if (File.Exists(outputFile))
            {
                WriteWarning($"Output file already exists: {outputFile}. Skipping...");
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

            WriteHostMessage(
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

            Logger.LogDebug("Executing ffmpeg with arguments: {Args}", string.Join(" ", ffmpegArgs));

            var result = ExecutableService.ExecuteAsync("ffmpeg", ffmpegArgs, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();

            if (result.ExitCode != 0)
            {
                var msg = $"ffmpeg failed with exit code {result.ExitCode} for output file: {outputFile}";
                if (!string.IsNullOrWhiteSpace(result.ErrorOutput))
                    msg += ". " + result.ErrorOutput.Trim();
                throw new InvalidOperationException(msg);
            }

            WriteHostMessage($"Successfully created: {outputFile}", ConsoleColor.Green);
            outputFiles.Add(outputFile);
        }

        foreach (var path in outputFiles)
            WriteObject(path);
    }

    private string? ResolveOutputDirectory(string resolvedInputPath) =>
        PathHelper.ResolveOutputDirectory(
            OutputPath,
            resolvedInputPath,
            SessionState.Path.CurrentLocation.Path,
            path =>
            {
                var ok = PathResolver.TryResolveOutputPath(path, out var r);
                return (ok, r);
            });

}
