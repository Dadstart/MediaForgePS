using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Orchestrates season processing for a TV series: creates folder structure, scans TVDb, copies episodes, and optionally extracts chapters and captions.
/// </summary>
/// <remarks>
/// This cmdlet is a high-level workflow that ties together season scanning, video copy, and optional chapter/caption extraction.
/// Use this when you want a one-stop command to prepare a full season for further processing or media library import.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "SeriesProcessing")]
[OutputType(typeof(void))]
public class InvokeSeriesProcessingCommand : CmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    /// <summary>
    /// Series title used for TVDb lookup and output folder/file naming.
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Series title used for TVDb lookup and for naming folders/files.")]
    [ValidateNotNullOrEmpty]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Season number to process (1-based).
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Season number to process (1-based).")]
    [ValidateRange(1, 9999)]
    public int Season { get; set; }

    /// <summary>
    /// First episode number in the input set, used when TVDb episodes start later or files begin mid-season.
    /// </summary>
    [Parameter(HelpMessage = "First episode number in the input set (default 1).")]
    [ValidateRange(1, 1000)]
    public int EpisodeStart { get; set; } = 1;

    /// <summary>
    /// One or more root folders containing source video files for the season.
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Root folder(s) containing source video files for the season.")]
    [ValidateNotNullOrEmpty]
    public string[] InputPath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// File name patterns (wildcards) used to find episode files under InputPath.
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "File name patterns (wildcards) used to find episode files under InputPath.")]
    [ValidateNotNullOrEmpty]
    public string[] FilePatterns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Minimum file size in bytes required for a file to be treated as a candidate episode.
    /// </summary>
    [Parameter(HelpMessage = "Minimum file size in bytes required to treat a file as an episode (default 1 GB).")]
    [ValidateRange(0, long.MaxValue)]
    public long MinimumFileSize { get; set; } = 1L * 1024 * 1024 * 1024;

    /// <summary>
    /// Root output directory; when set, output is written to OutputPath\Title\Season XX.
    /// </summary>
    [Parameter(HelpMessage = "Root output directory. When set, output is written to OutputPath\\Title\\Season XX.")]
    [ValidateNotNullOrEmpty]
    public string? OutputPath { get; set; }

    /// <summary>
    /// Optional TVDb series URL used as a starting point for season scans.
    /// </summary>
    [Parameter(HelpMessage = "Optional TVDb series URL used as a starting point for season scans.")]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeriesUrl { get; set; }

    /// <summary>
    /// Optional TVDb season URL; when omitted, constructed from TvDbSeriesUrl and Season.
    /// </summary>
    [Parameter(HelpMessage = "Optional TVDb season URL; when omitted, constructed from TvDbSeriesUrl and Season.")]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeasonUrl { get; set; }

    /// <summary>
    /// When specified, extracts chapter files for copied episodes.
    /// </summary>
    [Parameter(HelpMessage = "When specified, extracts chapter files for copied episodes.")]
    public SwitchParameter ExtractChapters { get; set; }

    /// <summary>
    /// When specified, skips caption extraction after copying episodes (chapters may still be extracted).
    /// </summary>
    [Parameter(HelpMessage = "Skip caption extraction after copying episodes (chapters may still be extracted).")]
    public SwitchParameter SkipCaptionExtraction { get; set; }

    /// <summary>
    /// When specified, skips OCR conversion of image-based captions (SUP, SUB).
    /// </summary>
    [Parameter(HelpMessage = "Skip OCR conversion of image captions to SRT.")]
    public SwitchParameter SkipOcr { get; set; }

    /// <summary>
    /// When specified, skips the SRT repair step during default OCR processing. Has no effect when -SkipOcr is specified.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair during OCR processing.")]
    public SwitchParameter SkipRepair { get; set; }

    private const int DefaultOcrThrottleLimit = 10;

    private ISeriesProcessingService? _seriesProcessingService;
    private ISeriesProcessingService SeriesProcessingService => _seriesProcessingService ??= ModuleServices.GetRequiredService<ISeriesProcessingService>();

    private IExecutableService? _executableService;
    private IPathResolver? _pathResolver;

    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Executes the end-to-end season processing workflow.
    /// </summary>
    protected override void Process()
    {
        WriteHostMessage($"Starting series processing for '{Title}' Season {Season:D2}", ConsoleColor.Cyan);
        WriteVerbose($"Starting series processing for '{Title}' season {Season}.");

        var seasonUrl = EnsureSeasonUrl(TvDbSeasonUrl, Season);
        WriteHostMessage("Step 1: Creating directory structure...", ConsoleColor.Cyan);
        var directoryStructure = SeriesProcessingService.NewProcessingDirectoryStructure(this, Title, Season, basePath: OutputPath);
        if (string.IsNullOrWhiteSpace(directoryStructure.SeasonDir))
            return;
        WriteHostMessage($"  Season directory: {directoryStructure.SeasonDir}", ConsoleColor.Gray);

        WriteHostMessage(string.Empty);
        WriteHostMessage("Step 2: Scanning season (TVDb)...", ConsoleColor.Cyan);
        var episodes = SeriesProcessingService.InvokeSeasonScan(this, Season, TvDbSeriesUrl, seasonUrl);
        if (episodes.Count == 0)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("Season scanning failed. Cannot proceed without episode information."),
                "SeasonScanFailed",
                ErrorCategory.InvalidData,
                null));
            return;
        }
        WriteHostMessage($"  Found {episodes.Count} episode(s)", ConsoleColor.Green);

        WriteHostMessage(string.Empty);
        WriteHostMessage("Step 3: Copying video files...", ConsoleColor.Cyan);
        var copiedFiles = SeriesProcessingService.InvokeVideoCopy(
            this,
            new VideoCopyRequest(
                InputPath,
                directoryStructure.SeasonDir,
                Title,
                Season,
                episodes,
                FilePatterns,
                EpisodeStart,
                MinimumFileSize));

        if (copiedFiles.Count == 0)
        {
            var minSizeHint = MinimumFileSize > 0
                ? $" (minimum file size: {MinimumFileSize / (1024 * 1024)} MB)"
                : string.Empty;
            WriteError(new ErrorRecord(
                new InvalidOperationException(
                    "Video copying failed. No files were copied. Check that InputPath contains matching files for FilePatterns and that files exceed the minimum size" + minSizeHint + "."),
                "VideoCopyFailed",
                ErrorCategory.InvalidData,
                null));
            return;
        }
        WriteHostMessage($"  Copied {copiedFiles.Count} file(s)", ConsoleColor.Green);

        if (ExtractChapters.IsPresent)
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage("Step 4: Extracting chapters...", ConsoleColor.Cyan);
            var chapterStats = SeriesProcessingService.InvokeChapterExtractionPhase(this, directoryStructure.SeasonDir, copiedFiles);
            WriteHostMessage($"  Processed: {chapterStats.Processed}, failed: {chapterStats.Failed}, total: {chapterStats.Total}", ConsoleColor.Green);
            WriteVerbose($"Chapter extraction - processed: {chapterStats.Processed}, failed: {chapterStats.Failed}, total: {chapterStats.Total}.");
        }

        if (!SkipCaptionExtraction.IsPresent)
        {
            WriteHostMessage(string.Empty);
            var stepNum = ExtractChapters.IsPresent ? 5 : 4;
            WriteHostMessage($"Step {stepNum}: Extracting captions...", ConsoleColor.Cyan);
            var captionStats = SeriesProcessingService.InvokeCaptionExtractionPhase(this, directoryStructure.SeasonDir, copiedFiles);
            WriteHostMessage($"  Processed: {captionStats.Processed}, failed: {captionStats.Failed}, total: {captionStats.Total}", ConsoleColor.Green);
            WriteVerbose($"Caption extraction - processed: {captionStats.Processed}, failed: {captionStats.Failed}, total: {captionStats.Total}.");

            if (!SkipOcr.IsPresent)
            {
                var extractedCaptionPaths = captionStats.ExtractedCaptionPaths;
                if (extractedCaptionPaths.Count > 0)
                {
                    var imagePaths = SubtitlePathHelper.GetImageSubtitlePaths(extractedCaptionPaths);
                    var srtPathsFromCaptions = SubtitlePathHelper.GetSrtPaths(extractedCaptionPaths);

                    if (imagePaths.Count > 0 || srtPathsFromCaptions.Count > 0)
                    {
                        WriteHostMessage("  Running OCR and repair on extracted captions...", ConsoleColor.Cyan);

                        var allSrtPaths = SubtitleOcrRepairWorkflow.Run(
                            this,
                            Logger,
                            ExecutableService,
                            PathResolver,
                            imagePaths,
                            srtPathsFromCaptions,
                            performOcr: true,
                            DefaultOcrThrottleLimit,
                            shouldRepair: !SkipRepair.IsPresent,
                            backupPath: null);

                        if (allSrtPaths == null)
                            return;

                        if (allSrtPaths.Count == 0)
                            WriteHostMessage("  No SRT files to repair (only non-SRT formats were extracted).", ConsoleColor.Green);
                        else
                            WriteHostMessage("  Caption OCR and repair completed.", ConsoleColor.Green);
                    }
                }
            }
        }

        WriteHostMessage(string.Empty);
        WriteHostMessage($"Series processing completed for '{Title}' Season {Season:D2}.", ConsoleColor.Green);
        WriteVerbose($"Series processing completed for '{Title}' season {Season}.");
    }

    /// <summary>
    /// Ensures a TVDb season URL points at a specific season, appending /{season} when necessary.
    /// </summary>
    /// <param name="tvDbSeasonUrl">Base or season-specific TVDb season URL.</param>
    /// <param name="season">Season number.</param>
    /// <returns>Normalized season URL or null when tvDbSeasonUrl is empty.</returns>
    public static string? EnsureSeasonUrl(string? tvDbSeasonUrl, int season)
    {
        if (string.IsNullOrWhiteSpace(tvDbSeasonUrl))
            return null;

        var trimmed = tvDbSeasonUrl.TrimEnd('/');
        return char.IsDigit(trimmed[^1]) ? trimmed : $"{trimmed}/{season}";
    }
}
