using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Gets audio streams for conversion
/// </summary>
[Cmdlet(VerbsCommon.Get, "AudioStreams")]
[OutputType(typeof(AudioTrackMapping[]))]
public class GetAudioTrackMappingsCommand : CmdletBase
{
    /// <summary>
    /// Path to the input media file
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to the input media file")]
    [ValidateNotNullOrEmpty]
    public string InputPath { get; set; } = string.Empty;

    private IPathResolver? _pathResolver;
    private IMediaReaderService? _mediaReaderService;
    private IAudioTrackMappingService? _audioTrackMappingService;

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Media reader service instance for retrieving media file information.
    /// </summary>
    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    /// <summary>
    /// Audio track mapping service instance for creating audio track mappings.
    /// </summary>
    private IAudioTrackMappingService AudioTrackMappingService => _audioTrackMappingService ??= ModuleServices.GetRequiredService<IAudioTrackMappingService>();

    /// <summary>
    /// Processes the media file conversion request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Get-AudioStreams request for path: {Path}", InputPath);

        if (!TryResolveInputPath(PathResolver, InputPath, out var resolvedPath))
            return;

        Logger.LogDebug("Reading media file information: {ResolvedPath}", resolvedPath);
        if (!TryGetMediaFile(MediaReaderService, resolvedPath, out var mediaFile))
            return;

        var mappings = AudioTrackMappingService.CreateMappings(mediaFile);
        WriteObject(mappings);
    }
}

