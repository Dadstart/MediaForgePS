using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Retrieves detailed information about a media file, including format, streams, and chapters.
/// </summary>
/// <remarks>
/// This cmdlet uses ffprobe to analyze media files and returns a <see cref="MediaFile"/> object
/// containing comprehensive metadata about the file's structure and content.
/// </remarks>
[Cmdlet(VerbsCommon.Get, "MediaFile")]
[OutputType(typeof(MediaFile))]
public class GetMediaFileCommand : CmdletBase
{
    /// <summary>
    /// Path to the media file to analyze.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to the media file")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    private IMediaReaderService? _mediaReaderService;
    private IPathResolver? _pathResolver;

    /// <summary>
    /// Media reader service instance for retrieving media file information.
    /// </summary>
    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Processes the media file path, resolves it, validates existence, and retrieves media information.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Get-MediaFile request for path: {Path}", Path);

        if (!TryResolveInputPath(PathResolver, Path, out var resolvedPath))
            return;

        Logger.LogDebug("Reading media file information: {ResolvedPath}", resolvedPath);
        if (!TryGetMediaFile(MediaReaderService, resolvedPath, out var mediaFile))
            return;

        Logger.LogInformation("Successfully retrieved media file: {ResolvedPath}", resolvedPath);
        WriteObject(mediaFile);
    }
}

