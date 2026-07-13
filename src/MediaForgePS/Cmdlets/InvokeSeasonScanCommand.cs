using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Retrieves TVDb episode metadata for a season.
/// </summary>
/// <remarks>
/// Returns <see cref="TvDbEpisodeInfo"/> objects (Id, SeasonNumber, Title, EpisodeNumber) used by
/// <see cref="InvokeVideoCopyCommand"/>, <see cref="SplitSeriesChaptersCommand"/>, and <see cref="InvokeSeriesProcessingCommand"/>.
/// Requires network access to thetvdb.com. When TvDbSeasonUrl is omitted, it is built from TvDbSeriesUrl and Season.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "SeasonScan")]
[OutputType(typeof(TvDbEpisodeInfo))]
public class InvokeSeasonScanCommand : CmdletBase
{
    /// <summary>
    /// Season number to scan (1-based).
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Season number to scan (1-based).")]
    [ValidateRange(1, 1000)]
    public int Season { get; set; }

    /// <summary>
    /// Optional TVDb series URL used as a starting point for the scan.
    /// </summary>
    [Parameter(HelpMessage = "Optional TVDb series URL used as a starting point for the scan.")]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeriesUrl { get; set; }

    /// <summary>
    /// Optional TVDb season URL; when omitted, constructed from TvDbSeriesUrl and Season.
    /// </summary>
    [Parameter(HelpMessage = "Optional TVDb season URL; when omitted, constructed from TvDbSeriesUrl and Season.")]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeasonUrl { get; set; }

    private ISeriesProcessingService? _seriesProcessingService;
    private ISeriesProcessingService SeriesProcessingService => _seriesProcessingService ??= ModuleServices.GetRequiredService<ISeriesProcessingService>();

    /// <summary>
    /// Performs the season scan and writes TvDbEpisodeInfo objects to the pipeline.
    /// </summary>
    protected override void Process()
    {
        var seasonUrl = InvokeSeriesProcessingCommand.EnsureSeasonUrl(TvDbSeasonUrl, Season);
        var episodes = SeriesProcessingService.InvokeSeasonScan(CmdletIO, Season, TvDbSeriesUrl, seasonUrl);
        if (episodes.Count == 0)
        {
            WriteWarning($"No episode information returned for season {Season}.");
            return;
        }

        WriteObject(episodes, true);
    }
}
