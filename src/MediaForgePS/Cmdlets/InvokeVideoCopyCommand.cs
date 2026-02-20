using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;

namespace Dadstart.Labs.MediaForge.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "VideoCopy")]
[OutputType(typeof(string))]
public class InvokeVideoCopyCommand : CmdletBase
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

    [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = Array.Empty<string>();

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string[] FilePatterns { get; set; } = Array.Empty<string>();

    [Parameter]
    [ValidateRange(1, long.MaxValue)]
    public long MinimumFileSize { get; set; } = 1L * 1024 * 1024 * 1024;

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Destination { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public TvDbEpisodeInfo[] Episodes { get; set; } = Array.Empty<TvDbEpisodeInfo>();

    private readonly List<string> _allPaths = new();
    private ISeriesProcessingService? _seriesProcessingService;
    private ISeriesProcessingService SeriesProcessingService => _seriesProcessingService ??= ModuleServices.GetRequiredService<ISeriesProcessingService>();

    protected override void Process()
    {
        foreach (var path in Path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                _allPaths.Add(path);
        }
    }

    protected override void End()
    {
        if (_allPaths.Count == 0)
        {
            WriteWarning("No input paths were provided.");
            return;
        }

        var copied = SeriesProcessingService.InvokeVideoCopy(
            this,
            new VideoCopyRequest(
                _allPaths,
                Destination,
                Title,
                Season,
                Episodes,
                FilePatterns,
                EpisodeStart,
                MinimumFileSize));

        WriteObject(copied, true);
    }
}
