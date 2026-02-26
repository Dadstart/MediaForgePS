using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;

namespace Dadstart.Labs.MediaForge.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "SeriesProcessing")]
[OutputType(typeof(void))]
public class InvokeSeriesProcessingCommand : CmdletBase
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

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string[] InputPath { get; set; } = Array.Empty<string>();

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string[] FilePatterns { get; set; } = Array.Empty<string>();

    [Parameter]
    [ValidateRange(0, long.MaxValue)]
    public long MinimumFileSize { get; set; } = 1L * 1024 * 1024 * 1024;

    /// <summary>Root output directory. When set, output is written to OutputPath\Title\Season XX.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? OutputPath { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeriesUrl { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeasonUrl { get; set; }

    [Parameter]
    public SwitchParameter ExtractChapters { get; set; }

    [Parameter]
    public SwitchParameter SkipCaptionExtraction { get; set; }

    private ISeriesProcessingService? _seriesProcessingService;
    private ISeriesProcessingService SeriesProcessingService => _seriesProcessingService ??= ModuleServices.GetRequiredService<ISeriesProcessingService>();

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
        }

        WriteHostMessage(string.Empty);
        WriteHostMessage($"Series processing completed for '{Title}' Season {Season:D2}.", ConsoleColor.Green);
        WriteVerbose($"Series processing completed for '{Title}' season {Season}.");
    }

    public static string? EnsureSeasonUrl(string? tvDbSeasonUrl, int season)
    {
        if (string.IsNullOrWhiteSpace(tvDbSeasonUrl))
            return null;

        var trimmed = tvDbSeasonUrl.TrimEnd('/');
        return char.IsDigit(trimmed[^1]) ? trimmed : $"{trimmed}/{season}";
    }
}
