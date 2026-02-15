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
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = Array.Empty<string>();

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string[] FilePatterns { get; set; } = Array.Empty<string>();

    [Parameter(Mandatory = true)]
    [ValidateRange(1, 1000)]
    public int Season { get; set; }

    [Parameter]
    [ValidateRange(1, 1000)]
    public int EpisodeStart { get; set; } = 1;

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

    [Parameter]
    [ValidateRange(0, long.MaxValue)]
    public long MinimumFileSize { get; set; } = 1L * 1024 * 1024 * 1024;

    private ISeriesProcessingService? _seriesProcessingService;
    private ISeriesProcessingService SeriesProcessingService => _seriesProcessingService ??= ModuleServices.GetRequiredService<ISeriesProcessingService>();

    protected override void Process()
    {
        WriteVerbose($"Starting series processing for '{Title}' season {Season}.");

        var seasonUrl = EnsureSeasonUrl(TvDbSeasonUrl, Season);
        var directoryStructure = SeriesProcessingService.NewProcessingDirectoryStructure(this, Title, Season);
        if (string.IsNullOrWhiteSpace(directoryStructure.SeasonDir))
            return;

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

        var copiedFiles = SeriesProcessingService.InvokeVideoCopy(
            this,
            new VideoCopyRequest(
                Path,
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
                    "Video copying failed. No files were copied. Check that Path contains matching files for FilePatterns and that files exceed the minimum size" + minSizeHint + "."),
                "VideoCopyFailed",
                ErrorCategory.InvalidData,
                null));
            return;
        }

        if (ExtractChapters.IsPresent)
        {
            var chapterStats = SeriesProcessingService.InvokeChapterExtractionPhase(this, directoryStructure.SeasonDir, copiedFiles);
            WriteVerbose($"Chapter extraction - processed: {chapterStats.Processed}, failed: {chapterStats.Failed}, total: {chapterStats.Total}.");
        }

        if (!SkipCaptionExtraction.IsPresent)
        {
            var captionStats = SeriesProcessingService.InvokeCaptionExtractionPhase(this, directoryStructure.SeasonDir, copiedFiles);
            WriteVerbose($"Caption extraction - processed: {captionStats.Processed}, failed: {captionStats.Failed}, total: {captionStats.Total}.");
        }

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
