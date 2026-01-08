using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dadstart.Labs.MediaForge.Models;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Service for creating audio track mappings from media files.
/// </summary>
public class AudioTrackMappingService : IAudioTrackMappingService
{
    private readonly ILogger<AudioTrackMappingService> _logger;

    public AudioTrackMappingService(ILogger<AudioTrackMappingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates audio track mappings for English audio streams in the media file.
    /// </summary>
    /// <param name="mediaFile">The media file to analyze.</param>
    /// <returns>An array of audio track mappings for English audio streams.</returns>
    public AudioTrackMapping[] CreateMappings(MediaFile mediaFile)
    {
        ArgumentNullException.ThrowIfNull(mediaFile);

        // Filter for English audio streams
        var englishAudioStreams = mediaFile.Streams
            .Where(s => string.Equals(s.Type, "audio", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(s.Language, "eng", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (englishAudioStreams.Count == 0)
        {
            _logger.LogInformation("No English audio streams found in: {Path}", mediaFile.Path);
            return Array.Empty<AudioTrackMapping>();
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
            _logger.LogDebug("Applying swap logic: swapping destination indices for DTS and 6+ channel AAC");
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

        _logger.LogInformation("Successfully created {Count} audio track mappings for: {Path}", mappings.Count, mediaFile.Path);
        return mappings.ToArray();
    }

    /// <summary>
    /// Parses the channel count from a stream's raw JSON.
    /// </summary>
    /// <param name="rawJson">The raw JSON string from the stream.</param>
    /// <returns>The channel count, or 0 if not found.</returns>
    public static int ParseChannelCount(string rawJson)
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
