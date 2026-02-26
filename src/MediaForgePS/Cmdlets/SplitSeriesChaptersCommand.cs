using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Splits a video file into episodes based on chapter ranges and TVDb episode IDs.
/// </summary>
[Cmdlet(VerbsCommon.Split, "SeriesChapters", DefaultParameterSetName = "ByPath")]
[OutputType(typeof(string[]))]
public class SplitSeriesChaptersCommand : CmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Title { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateRange(1, 1000)]
    public int Season { get; set; }

    [Parameter]
    [ValidateRange(1, 1000)]
    public int EpisodeStart { get; set; } = 1;

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string InputFile { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNull]
    public object[] ChapterRanges { get; set; } = [];

    [Parameter]
    public string? OutputPath { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeriesUrl { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeasonUrl { get; set; }

    private readonly List<string> _inputFiles = [];
    private IMediaReaderService? _mediaReaderService;
    private IExecutableService? _executableService;
    private IPathResolver? _pathResolver;
    private ISeriesProcessingService? _seriesProcessingService;

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();
    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();
    private ISeriesProcessingService SeriesProcessingService => _seriesProcessingService ??= ModuleServices.GetRequiredService<ISeriesProcessingService>();

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

        var seasonUrl = InvokeSeriesProcessingCommand.EnsureSeasonUrl(TvDbSeasonUrl, Season);
        var episodes = SeriesProcessingService.InvokeSeasonScan(this, Season, TvDbSeriesUrl, seasonUrl)
            .OrderBy(e => e.EpisodeNumber)
            .ToArray();
        if (episodes.Length == 0)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("No TVDb episode information returned for the requested season."),
                "NoTvDbEpisodes",
                ErrorCategory.InvalidData,
                Season));
            return;
        }

        var requiredEpisodeCount = (EpisodeStart - 1) + normalizedRanges.Count;
        if (episodes.Length < requiredEpisodeCount)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException(
                    $"Not enough episode IDs found. Need {requiredEpisodeCount}, but only found {episodes.Length}."),
                "InsufficientTvDbEpisodes",
                ErrorCategory.InvalidData,
                Season));
            return;
        }

        foreach (var inputPath in _inputFiles)
        {
            try
            {
                SplitChaptersForFile(inputPath, normalizedRanges, episodes);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "SplitSeriesChaptersFailed", ErrorCategory.OperationStopped, inputPath));
            }
        }
    }

    private void SplitChaptersForFile(
        string inputPath,
        List<(int Start, int End, string? OutputName)> ranges,
        IReadOnlyList<TvDbEpisodeInfo> episodes)
    {
        if (!TryResolveInputPath(PathResolver, inputPath, out var resolvedInputPath))
            return;

        var outputDir = ChapterSplitHelper.ResolveOutputDirectory(
            PathResolver,
            OutputPath,
            resolvedInputPath,
            SessionState.Path.CurrentLocation.Path);
        if (string.IsNullOrEmpty(outputDir))
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("Could not resolve output directory."),
                "OutputPathResolutionFailed",
                ErrorCategory.InvalidOperation,
                OutputPath));
            return;
        }

        WriteHostMessage($"Getting chapter information from: {resolvedInputPath}", ConsoleColor.Cyan);
        var mediaFile = ChapterSplitHelper.ReadMediaFile(MediaReaderService, resolvedInputPath);
        if (!ChapterSplitHelper.TryGetChapters(this, resolvedInputPath, mediaFile, out var chapters))
            return;

        WriteHostMessage($"Found {chapters.Length} chapters", ConsoleColor.Green);

        var inputExtension = Path.GetExtension(resolvedInputPath);
        if (string.IsNullOrWhiteSpace(inputExtension))
            inputExtension = ".mkv";

        var outputFiles = ChapterSplitHelper.SplitChapterRanges(
            this,
            Logger,
            ExecutableService,
            resolvedInputPath,
            outputDir,
            ranges,
            chapters,
            (rangeIndex, range) =>
            {
                if (!string.IsNullOrWhiteSpace(range.OutputName))
                    return range.OutputName + inputExtension;

                var episodeIndex = (EpisodeStart - 1) + rangeIndex;
                var episodeNumber = EpisodeStart + rangeIndex;
                var tvDbEpisode = episodes[episodeIndex];
                return $"{Title} {{tvdb {tvDbEpisode.Id}}} S{Season:D2}E{episodeNumber:D2}{inputExtension}";
            },
            WriteHostMessage);

        foreach (var path in outputFiles)
            WriteObject(path);
    }
}
