using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;

namespace Dadstart.Labs.MediaForge.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "SeasonScan")]
[OutputType(typeof(TvDbEpisodeInfo))]
public class InvokeSeasonScanCommand : CmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateRange(1, 1000)]
    public int Season { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeriesUrl { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? TvDbSeasonUrl { get; set; }

    private ISeriesProcessingService? _seriesProcessingService;
    private ISeriesProcessingService SeriesProcessingService => _seriesProcessingService ??= ModuleServices.GetRequiredService<ISeriesProcessingService>();

    protected override void Process()
    {
        var seasonUrl = InvokeSeriesProcessingCommand.EnsureSeasonUrl(TvDbSeasonUrl, Season);
        var episodes = SeriesProcessingService.InvokeSeasonScan(this, Season, TvDbSeriesUrl, seasonUrl);
        if (episodes.Count == 0)
        {
            WriteWarning($"No episode information returned for season {Season}.");
            return;
        }

        WriteObject(episodes, true);
    }
}
