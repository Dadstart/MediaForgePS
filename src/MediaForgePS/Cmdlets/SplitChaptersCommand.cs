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
[Cmdlet(VerbsCommon.Split, "Chapters", DefaultParameterSetName = "ByPath")]
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
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNull]
    public object[] ChapterRanges { get; set; } = [];

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

    private static void WriteHostMessage(PSCmdlet cmdlet, string message, ConsoleColor? foregroundColor = null)
    {
        var hostMsg = new HostInformationMessage { Message = message };
        if (foregroundColor.HasValue)
            hostMsg.ForegroundColor = foregroundColor.Value;
        cmdlet.WriteInformation(new InformationRecord(hostMsg, "PSHOST"));
    }

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

        var normalizedRanges = NormalizeChapterRanges(ChapterRanges);
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

    private static List<(int Start, int End, string? OutputName)> NormalizeChapterRanges(object[] chapterRanges)
    {
        var list = new List<(int, int, string?)>();
        for (var i = 0; i < chapterRanges.Length; i++)
        {
            var item = chapterRanges[i];
            if (item is ChapterRange cr)
            {
                list.Add((cr.Start, cr.End, cr.OutputName));
                continue;
            }

            var psObj = item as PSObject;
            var startProp = psObj?.Properties["Start"]?.Value;
            var endProp = psObj?.Properties["End"]?.Value;
            var outputNameProp = psObj?.Properties["OutputName"]?.Value;

            if (startProp == null || endProp == null)
            {
                throw new ArgumentException(
                    $"Chapter range at index {i} is missing Start or End property.");
            }

            if (!LanguagePrimitives.TryConvertTo(startProp, out int start) ||
                !LanguagePrimitives.TryConvertTo(endProp, out int end))
            {
                throw new ArgumentException(
                    $"Chapter range at index {i}: Start and End must be integers.");
            }

            list.Add((start, end, outputNameProp?.ToString()));
        }

        return list;
    }

    private void SplitChaptersForFile(string inputPath, List<(int Start, int End, string? OutputName)> ranges)
    {
        if (!PathResolver.TryResolveInputPath(inputPath, out var resolvedInputPath))
        {
            WriteError(new ErrorRecord(
                new FileNotFoundException("Input file not found.", inputPath),
                "FileNotFound",
                ErrorCategory.ObjectNotFound,
                inputPath));
            return;
        }

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

        WriteHostMessage(this, $"Getting chapter information from: {resolvedInputPath}", ConsoleColor.Cyan);

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

        var chapters = mediaFile.Chapters;
        WriteHostMessage(this, $"Found {chapters.Length} chapters", ConsoleColor.Green);

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

            var startTimeCode = FormatTimeCode(startTime);
            var durationTimeCode = FormatTimeCode(duration);

            WriteHostMessage(this,
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

            WriteHostMessage(this, $"Successfully created: {outputFile}", ConsoleColor.Green);
            outputFiles.Add(outputFile);
        }

        foreach (var path in outputFiles)
            WriteObject(path);
    }

    private string? ResolveOutputDirectory(string resolvedInputPath)
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
            return Path.GetDirectoryName(resolvedInputPath);

        var pathToResolve = Path.Combine(OutputPath.Trim(), "dummy.mkv");
        if (!PathResolver.TryResolveOutputPath(pathToResolve, out var resolved))
            resolved = Path.GetFullPath(Path.Combine(SessionState.Path.CurrentLocation.Path, OutputPath.Trim(), "dummy.mkv"));

        var dir = Path.GetDirectoryName(resolved);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return dir;
    }

    private static string FormatTimeCode(double seconds)
    {
        var hours = (int)Math.Floor(seconds / 3600);
        var minutes = (int)Math.Floor((seconds % 3600) / 60);
        var secs = seconds % 60;
        return $"{hours:D2}:{minutes:D2}:{secs:00.000}";
    }
}
