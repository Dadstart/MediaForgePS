using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Copies episode video files into a destination folder using TVDb episode metadata for naming.
/// </summary>
/// <remarks>
/// Lower-level step used by <see cref="InvokeSeriesProcessingCommand"/>.
/// Searches each -Path root (top directory only, not recursive) for files matching -FilePatterns
/// larger than -MinimumFileSize. The Nth matched file maps to TVDb episode (EpisodeStart - 1) + N.
/// Pipeline -Path values are collected during Process and executed in End.
/// Supports -WhatIf and -Confirm. Use -Force to overwrite existing destination files.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "VideoCopy", SupportsShouldProcess = true)]
[OutputType(typeof(string))]
public class InvokeVideoCopyCommand : ProgressCmdletBase
{
    /// <summary>
    /// Series title used for destination file naming.
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Series title used for destination file naming.")]
    [ValidateNotNullOrEmpty]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Season number to copy (1-based).
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Season number to copy (1-based).")]
    [ValidateRange(1, 1000)]
    public int Season { get; set; }

    /// <summary>
    /// First episode number in the input set, used when files begin mid-season.
    /// </summary>
    [Parameter(HelpMessage = "First episode number in the input set (default 1).")]
    [ValidateRange(1, 1000)]
    public int EpisodeStart { get; set; } = 1;

    /// <summary>
    /// One or more root folders containing source video files.
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Root folder(s) containing source video files.")]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <summary>
    /// File name patterns (wildcards) used to find episode files under Path.
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "File name patterns (wildcards) used to find episode files under Path.")]
    [ValidateNotNullOrEmpty]
    public string[] FilePatterns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Minimum file size in bytes required for a file to be treated as a candidate episode.
    /// </summary>
    [Parameter(HelpMessage = "Minimum file size in bytes required to treat a file as an episode (default 1 GB).")]
    [ValidateRange(1, long.MaxValue)]
    public long MinimumFileSize { get; set; } = 1L * 1024 * 1024 * 1024;

    /// <summary>
    /// Destination directory where copied episode files are written.
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "Destination directory where copied episode files are written.")]
    [ValidateNotNullOrEmpty]
    public string Destination { get; set; } = string.Empty;

    /// <summary>
    /// TVDb episode metadata for the season, used to name and organize copied files.
    /// </summary>
    [Parameter(Mandatory = true, HelpMessage = "TVDb episode metadata for the season, used to name and organize copied files.")]
    [ValidateNotNull]
    public TvDbEpisodeInfo[] Episodes { get; set; } = Array.Empty<TvDbEpisodeInfo>();

    /// <summary>
    /// Overwrites destination files when they already exist.
    /// </summary>
    [Parameter(HelpMessage = "Overwrites destination files when they already exist.")]
    public SwitchParameter Force { get; set; }

    private readonly List<string> _allPaths = new();
    private ISeriesProcessingService? _seriesProcessingService;
    private ISeriesProcessingService SeriesProcessingService => _seriesProcessingService ??= ModuleServices.GetRequiredService<ISeriesProcessingService>();

    /// <summary>
    /// Collects all input paths from the pipeline.
    /// </summary>
    protected override void Process()
    {
        foreach (var path in Path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                _allPaths.Add(path);
        }
    }

    /// <summary>
    /// Executes the video copy operation and writes copied paths to the pipeline.
    /// </summary>
    protected override void End()
    {
        if (_allPaths.Count == 0)
        {
            WriteWarning("No input paths were provided.");
            return;
        }

        if (!ShouldProcess(Destination, $"Copy episode files for '{Title}' season {Season}"))
            return;

        var copied = SeriesProcessingService.InvokeVideoCopy(
            CmdletIO,
            new VideoCopyRequest(
                _allPaths,
                Destination,
                Title,
                Season,
                Episodes,
                FilePatterns,
                EpisodeStart,
                MinimumFileSize,
                Force.IsPresent));

        WriteObject(copied, true);
    }
}
