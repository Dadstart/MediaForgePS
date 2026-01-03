using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.Json;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
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

    private IFfmpegService? _ffmpegService;
    private IPathResolver? _pathResolver;
    private IPlatformService? _platformService;
    private IMediaReaderService? _mediaReaderService;

    /// <summary>
    /// Ffmpeg service instance for performing media file conversion.
    /// </summary>
    private IFfmpegService FfmpegService => _ffmpegService ??= ModuleServices.GetRequiredService<IFfmpegService>();

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Platform service instance for platform-specific operations.
    /// </summary>
    private IPlatformService PlatformService => _platformService ??= ModuleServices.GetRequiredService<IPlatformService>();

    /// <summary>
    /// Media reader service instance for retrieving media file information.
    /// </summary>
    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    /// <summary>
    /// Processes the media file conversion request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Get-AudioStreams request for path: {Path}", InputPath);

        string resolvedPath;
        if (!PathResolver.TryResolveInputPath(InputPath, out resolvedPath))
        {
            var errorRecord = new ErrorRecord(
                new FileNotFoundException($"Media file not found: {InputPath}"),
                "FileNotFound",
                ErrorCategory.ObjectNotFound,
                InputPath);
            WriteError(errorRecord);
            return;
        }

        try
        {
            Logger.LogDebug("Reading media file information: {ResolvedPath}", resolvedPath);
            var mediaFile = MediaReaderService.GetMediaFileAsync(resolvedPath, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            if (mediaFile is null)
            {
                Logger.LogWarning("Media file information is null for: {ResolvedPath}", resolvedPath);
                var errorRecord = new ErrorRecord(
                    new Exception($"Failed to get media file information: {resolvedPath}"),
                    "MediaFileReadFailed",
                    ErrorCategory.ReadError,
                    resolvedPath);
                WriteError(errorRecord);
                return;
            }

            // Filter for English audio streams
            var englishAudioStreams = mediaFile.Streams
                .Where(s => string.Equals(s.Type, "audio", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(s.Language, "eng", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (englishAudioStreams.Count == 0)
            {
                Logger.LogInformation("No English audio streams found in: {ResolvedPath}", resolvedPath);
                WriteObject(Array.Empty<AudioTrackMapping>());
                return;
            }

            // Parse channel counts and create mappings
            var mappings = new List<AudioTrackMapping>();
            int destinationIndex = 0;

            foreach (var stream in englishAudioStreams)
            {
                int channels = ParseChannelCount(stream.Raw);
                stream.Tags.TryGetValue("title", out var title);

                AudioTrackMapping mapping;
                if (string.Equals(stream.Codec, "dts", StringComparison.OrdinalIgnoreCase))
                {
                    // DTS: always copy
                    mapping = new CopyAudioTrackMapping(
                        title,
                        stream.Index,
                        stream.Index,
                        destinationIndex);
                }
                else
                {
                    // Determine encoding settings based on channel count
                    string codec = "aac";
                    int bitrate;
                    int destChannels;

                    if (channels >= 6)
                    {
                        bitrate = 384;
                        destChannels = 6;
                    }
                    else if (channels >= 2)
                    {
                        bitrate = 160;
                        destChannels = 2;
                    }
                    else
                    {
                        bitrate = 80;
                        destChannels = 1;
                    }

                    mapping = new EncodeAudioTrackMapping(
                        title,
                        stream.Index,
                        stream.Index,
                        destinationIndex,
                        codec,
                        bitrate,
                        destChannels);
                }

                mappings.Add(mapping);
                destinationIndex++;
            }

            // Apply swap logic: if first is DTS and second is 6+ channel AAC, swap destination indices
            if (mappings.Count >= 2 &&
                mappings[0] is CopyAudioTrackMapping &&
                mappings[1] is EncodeAudioTrackMapping encodeMapping &&
                string.Equals(encodeMapping.DestinationCodec, "aac", StringComparison.OrdinalIgnoreCase) &&
                encodeMapping.DestinationChannels >= 6)
            {
                Logger.LogDebug("Applying swap logic: swapping destination indices for DTS and 6+ channel AAC");
                var firstDestIndex = mappings[0].DestinationIndex;
                var secondDestIndex = mappings[1].DestinationIndex;

                // Swap by creating new instances with swapped destination indices
                if (mappings[0] is CopyAudioTrackMapping copyMapping)
                {
                    mappings[0] = new CopyAudioTrackMapping(
                        copyMapping.Title,
                        copyMapping.SourceStream,
                        copyMapping.SourceIndex,
                        secondDestIndex);
                }

                mappings[1] = new EncodeAudioTrackMapping(
                    encodeMapping.Title,
                    encodeMapping.SourceStream,
                    encodeMapping.SourceIndex,
                    firstDestIndex,
                    encodeMapping.DestinationCodec,
                    encodeMapping.DestinationBitrate,
                    encodeMapping.DestinationChannels);
            }

            Logger.LogInformation("Successfully created {Count} audio track mappings for: {ResolvedPath}", mappings.Count, resolvedPath);
            WriteObject(mappings.ToArray());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while processing media file: {ResolvedPath}", resolvedPath);
            var errorRecord = new ErrorRecord(
                ex,
                "MediaFileReadFailed",
                ErrorCategory.ReadError,
                resolvedPath);
            WriteError(errorRecord);
            return;
        }
    }

    /// <summary>
    /// Parses the channel count from a stream's raw JSON.
    /// </summary>
    /// <param name="rawJson">The raw JSON string from the stream.</param>
    /// <returns>The channel count, or 0 if not found.</returns>
    private static int ParseChannelCount(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return 0;

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;

            if (root.TryGetProperty("channels", out var channelsElement))
            {
                if (channelsElement.ValueKind == JsonValueKind.Number)
                    return channelsElement.GetInt32();
            }

            return 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}

