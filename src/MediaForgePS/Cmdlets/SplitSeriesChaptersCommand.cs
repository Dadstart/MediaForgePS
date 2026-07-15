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
/// Splits a video file into episode files by chapter ranges with TVDb-based naming.
/// </summary>
/// <remarks>
/// Output names follow: {Title} {tvdb Id} - s{season}e{episode}.{ext}.
/// Requires at least (EpisodeStart - 1) + rangeCount episodes from the TVDb scan.
/// </remarks>
[Cmdlet(VerbsCommon.Split, "SeriesChapters", DefaultParameterSetName = "ByPath")]
[OutputType(typeof(string[]))]
public class SplitSeriesChaptersCommand : CmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    /// <summary>
    /// Series title used in output file names.
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Series title used in output file names.")]
    [ValidateNotNullOrEmpty]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Season number represented by the input file (1-based).
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Season number represented by the input file (1-based).")]
    [ValidateRange(1, 1000)]
    public int Season { get; set; }

    /// <summary>
    /// First episode number mapped to the first chapter range.
    /// </summary>
    [Parameter(HelpMessage = "First episode number mapped to the first chapter range (default 1).")]
    [ValidateRange(1, 1000)]
    public int EpisodeStart { get; set; } = 1;

    /// <summary>
    /// Input video file to split into episodes.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Input video file to split into episodes.")]
    [ValidateNotNullOrEmpty]
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    /// Chapter ranges; each object must define Start (1-based), End (1-based inclusive), and optional OutputName.
    /// </summary>
    [Parameter(Mandatory = true, Position = 1, HelpMessage = "Chapter ranges with Start, End (1-based, inclusive) and optional OutputName.")]
    [ValidateNotNull]
    public object[] ChapterRanges { get; set; } = [];

    /// <summary>
    /// Output directory for episode files; defaults to the input file's directory when omitted.
    /// </summary>
    [Parameter(HelpMessage = "Output directory for episode files; defaults to the input file's directory when omitted.")]
    public string? OutputPath { get; set; }

    /// <summary>
    /// Optional TVDb series URL used as a starting point for fetching episode metadata.
    /// </summary>
    [Parameter(HelpMessage = "Optional TVDb series URL used as a starting point for fetching episode metadata.")]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeriesUrl { get; set; }

    /// <summary>
    /// Optional TVDb season URL; when omitted, constructed from TvDbSeriesUrl and Season.
    /// </summary>
    [Parameter(HelpMessage = "Optional TVDb season URL; when omitted, constructed from TvDbSeriesUrl and Season.")]
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

    /// <summary>
    /// Collects input files from the pipeline.
    /// </summary>
    protected override void Process()
    {
        if (!string.IsNullOrWhiteSpace(InputFile))
            _inputFiles.Add(InputFile);
    }

    /// <summary>
    /// Performs the split operation for each collected input file.
    /// </summary>
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
        var episodes = SeriesProcessingService.InvokeSeasonScan(CmdletIO, Season, TvDbSeriesUrl, seasonUrl, StoppingToken)
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

        var inputExtension = Path.GetExtension(resolvedInputPath);
        if (string.IsNullOrWhiteSpace(inputExtension))
            inputExtension = ".mkv";

        var outputFiles = ChapterSplitHelper.ExecuteSplitWorkflow(
            CmdletIO,
            Logger,
            MediaReaderService,
            ExecutableService,
            PathResolver,
            resolvedInputPath,
            OutputPath,
            ranges,
            (rangeIndex, range) =>
            {
                if (!string.IsNullOrWhiteSpace(range.OutputName))
                    return range.OutputName + inputExtension;

                var episodeIndex = (EpisodeStart - 1) + rangeIndex;
                var tvDbEpisode = episodes[episodeIndex];
                return Services.SeriesProcessing.SeriesProcessingService.BuildEpisodeFileName(Title, Season, tvDbEpisode, inputExtension);
            },
            WriteHostMessage,
            cancellationToken: StoppingToken);
        if (outputFiles == null)
            return;

        foreach (var path in outputFiles)
            WriteObject(path);
    }
}
