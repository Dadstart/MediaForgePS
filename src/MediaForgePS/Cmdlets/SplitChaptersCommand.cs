using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Splits a video file into multiple files based on chapter ranges.
/// </summary>
/// <remarks>
/// Chapter indices are 1-based (Start=1, End=1 is the first chapter).
/// Pipeline input is collected during Process and executed in End for batch splitting.
/// </remarks>
[Cmdlet(VerbsCommon.Split, "Chapters", DefaultParameterSetName = "ByRanges")]
[OutputType(typeof(string[]))]
public class SplitChaptersCommand : CmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    /// <summary>
    /// Path to the input video file.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path to the input video file to split.")]
    [ValidateNotNullOrEmpty]
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    /// Chapter ranges; each range has Start (1-based), End (1-based inclusive), and optional OutputName.
    /// </summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = "ByRanges", HelpMessage = "Chapter ranges with Start, End (1-based, inclusive) and optional OutputName.")]
    [ValidateNotNull]
    public object[] ChapterRanges { get; set; } = [];

    /// <summary>
    /// When specified, splits every chapter into its own file. Mutually exclusive with -ChapterRanges.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "AllChapters", HelpMessage = "Split every chapter into its own file (mutually exclusive with -ChapterRanges).")]
    public SwitchParameter AllChapters { get; set; }

    /// <summary>
    /// Directory where output files are saved; defaults to the input file's directory.
    /// </summary>
    [Parameter(Mandatory = false, HelpMessage = "Directory where output files are saved; defaults to the input file's directory.")]
    public string? OutputPath { get; set; }

    private readonly List<string> _inputFiles = [];
    private IMediaReaderService? _mediaReaderService;
    private IExecutableService? _executableService;
    private IPathResolver? _pathResolver;

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();
    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Collects input files from the pipeline.
    /// </summary>
    protected override void Process()
    {
        if (!string.IsNullOrWhiteSpace(InputFile))
            _inputFiles.Add(InputFile);
    }

    /// <summary>
    /// Performs chapter splitting for each collected input file.
    /// </summary>
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
                    WriteStandardError(ex, ErrorIds.SplitChaptersFailed, ErrorCategory.OperationStopped, inputPath);
                }
            }

            return;
        }

        var normalizedRanges = ChapterRangeHelper.NormalizeChapterRanges(ChapterRanges);
        if (normalizedRanges.Count == 0)
        {
            WriteError(new ErrorRecord(
                new ArgumentException("At least one valid chapter range with Start and End is required."),
                ErrorIds.InvalidChapterRanges,
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
                WriteStandardError(ex, ErrorIds.SplitChaptersFailed, ErrorCategory.OperationStopped, inputPath);
            }
        }
    }

    private void SplitAllChaptersForFile(string inputPath)
    {
        if (!TryResolveInputPath(PathResolver, inputPath, out var resolvedInputPath))
            return;

        WriteHostMessage($"Getting chapter information from: {resolvedInputPath}", ConsoleColor.Cyan);
        var mediaFile = ChapterSplitHelper.ReadMediaFile(MediaReaderService, resolvedInputPath, StoppingToken);
        if (!ChapterSplitHelper.TryGetChapters(CmdletIO, resolvedInputPath, mediaFile, out var chapters))
            return;
        WriteHostMessage($"Found {chapters.Length} chapters", ConsoleColor.Green);

        var ranges = BuildSingleChapterRanges(chapters.Length);
        var inputExtension = GetInputExtensionOrDefault(resolvedInputPath);
        var baseName = Path.GetFileNameWithoutExtension(resolvedInputPath);
        var outputFiles = ChapterSplitHelper.ExecuteSplitWorkflow(
            CmdletIO,
            Logger,
            MediaReaderService,
            ExecutableService,
            PathResolver,
            resolvedInputPath,
            OutputPath,
            ranges,
            (rangeIndex, range) => !string.IsNullOrWhiteSpace(range.OutputName)
                ? range.OutputName + inputExtension
                : $"{baseName}.split-{(rangeIndex + 1):D2}{inputExtension}",
            WriteHostMessage,
            chapters,
            StoppingToken);
        if (outputFiles == null)
            return;

        foreach (var path in outputFiles)
            WriteObject(path);
    }

    private void SplitChaptersForFile(string inputPath, List<(int Start, int End, string? OutputName)> ranges)
    {
        if (!TryResolveInputPath(PathResolver, inputPath, out var resolvedInputPath))
            return;

        var inputExtension = GetInputExtensionOrDefault(resolvedInputPath);
        var baseName = Path.GetFileNameWithoutExtension(resolvedInputPath);
        var outputFiles = ChapterSplitHelper.ExecuteSplitWorkflow(
            CmdletIO,
            Logger,
            MediaReaderService,
            ExecutableService,
            PathResolver,
            resolvedInputPath,
            OutputPath,
            ranges,
            (rangeIndex, range) => !string.IsNullOrWhiteSpace(range.OutputName)
                ? range.OutputName + inputExtension
                : $"{baseName}.split-{(rangeIndex + 1):D2}{inputExtension}",
            WriteHostMessage,
            cancellationToken: StoppingToken);
        if (outputFiles == null)
            return;

        foreach (var path in outputFiles)
            WriteObject(path);
    }

    private static List<(int Start, int End, string? OutputName)> BuildSingleChapterRanges(int chapterCount)
    {
        var ranges = new List<(int Start, int End, string? OutputName)>(chapterCount);
        for (var i = 1; i <= chapterCount; i++)
            ranges.Add((i, i, null));
        return ranges;
    }

    private static string GetInputExtensionOrDefault(string resolvedInputPath)
    {
        var extension = Path.GetExtension(resolvedInputPath);
        return string.IsNullOrWhiteSpace(extension) ? ".mkv" : extension;
    }
}
