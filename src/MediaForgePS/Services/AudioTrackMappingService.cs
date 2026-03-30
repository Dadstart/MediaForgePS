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
                    0, // SourceStream: input file index (always 0 for single input)
                    stream.Index, // SourceIndex: stream index within the file
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
                    0, // SourceStream: input file index (always 0 for single input)
                    stream.Index, // SourceIndex: stream index within the file
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
    /// Creates automatic audio mappings from selected streams for conversion workflows.
    /// </summary>
    /// <param name="streams">Selected streams to map.</param>
    /// <returns>Audio mappings for conversion workflows.</returns>
    public AudioTrackMapping[] CreateAutomaticMappings(IEnumerable<MediaStream> streams)
    {
        return CreateAutomaticMappingsFromStreams(streams);
    }

    /// <summary>
    /// Creates audio mappings for MKV directory batch encoding.
    /// </summary>
    /// <param name="mediaFile">Media file to analyze.</param>
    /// <returns>Array of conversion mappings for English audio streams.</returns>
    public AudioTrackMapping[] CreateDirectoryEncodeMappings(MediaFile mediaFile)
    {
        ArgumentNullException.ThrowIfNull(mediaFile);

        var audioStreams = mediaFile.Streams
            .Where(s => string.Equals(s.Type, "audio", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Index)
            .ToList();

        if (audioStreams.Count == 0)
            return Array.Empty<AudioTrackMapping>();

        var englishAudioStreams = audioStreams
            .Where(s => string.Equals(s.Language, "eng", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (englishAudioStreams.Count == 0)
            return Array.Empty<AudioTrackMapping>();

        var audioIndexLookup = audioStreams
            .Select((stream, index) => new { stream.Index, AudioIndex = index })
            .ToDictionary(entry => entry.Index, entry => entry.AudioIndex);

        var mappings = new List<AudioTrackMapping>();
        var destinationIndex = 0;

        var usePreferredSwap = englishAudioStreams.Count >= 2 &&
            IsDtsMaOrTrueHd(englishAudioStreams[0]) &&
            ParseChannelCount(englishAudioStreams[1].Raw) == 6;

        if (usePreferredSwap)
        {
            var secondStream = englishAudioStreams[1];
            var secondChannels = NormalizeChannelCount(ParseChannelCount(secondStream.Raw));
            secondStream.Tags.TryGetValue("title", out var secondTitle);

            mappings.Add(new EncodeAudioTrackMapping(
                secondTitle,
                0,
                audioIndexLookup[secondStream.Index],
                destinationIndex++,
                "aac",
                GetAacBitrate(secondChannels),
                secondChannels));

            var firstStream = englishAudioStreams[0];
            firstStream.Tags.TryGetValue("title", out var firstTitle);

            mappings.Add(new CopyAudioTrackMapping(
                firstTitle,
                0,
                audioIndexLookup[firstStream.Index],
                destinationIndex++));
        }
        else
        {
            var firstStream = englishAudioStreams[0];
            var firstChannels = NormalizeChannelCount(ParseChannelCount(firstStream.Raw));
            firstStream.Tags.TryGetValue("title", out var firstTitle);

            mappings.Add(new EncodeAudioTrackMapping(
                firstTitle,
                0,
                audioIndexLookup[firstStream.Index],
                destinationIndex++,
                "aac",
                GetAacBitrate(firstChannels),
                firstChannels));

            if (englishAudioStreams.Count >= 2)
            {
                var secondStream = englishAudioStreams[1];
                var secondChannels = NormalizeChannelCount(ParseChannelCount(secondStream.Raw));
                secondStream.Tags.TryGetValue("title", out var secondTitle);

                mappings.Add(new EncodeAudioTrackMapping(
                    secondTitle,
                    0,
                    audioIndexLookup[secondStream.Index],
                    destinationIndex++,
                    "aac",
                    GetAacBitrate(secondChannels),
                    secondChannels));
            }
        }

        foreach (var stream in englishAudioStreams.Skip(2))
        {
            var channels = NormalizeChannelCount(ParseChannelCount(stream.Raw));
            stream.Tags.TryGetValue("title", out var title);

            mappings.Add(new EncodeAudioTrackMapping(
                title,
                0,
                audioIndexLookup[stream.Index],
                destinationIndex++,
                "aac",
                GetAacBitrate(channels),
                channels));
        }

        return mappings.ToArray();
    }

    public static AudioTrackMapping[] CreateAutomaticMappingsFromStreams(IEnumerable<MediaStream> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);

        var mappings = new List<AudioTrackMapping>();
        var destinationIndex = 0;

        foreach (var stream in streams)
        {
            var channels = ParseChannelCount(stream.Raw);
            stream.Tags.TryGetValue("title", out var title);

            AudioTrackMapping mapping;
            var codecLower = stream.Codec.ToLowerInvariant();
            if ((codecLower == "dts" || codecLower == "truehd") &&
                channels >= 6 &&
                !string.Equals(stream.Profile, "dts", StringComparison.OrdinalIgnoreCase))
            {
                mapping = new CopyAudioTrackMapping(title, 0, stream.Index - 1, destinationIndex);
            }
            else
            {
                mapping = new EncodeAudioTrackMapping(title, 0, stream.Index - 1, destinationIndex, "aac", 0, channels);
            }

            mappings.Add(mapping);
            destinationIndex++;
        }

        if (mappings.Count >= 2 &&
            mappings[0] is CopyAudioTrackMapping copyMapping &&
            mappings[1] is EncodeAudioTrackMapping encodeMapping &&
            string.Equals(encodeMapping.DestinationCodec, "aac", StringComparison.OrdinalIgnoreCase) &&
            encodeMapping.DestinationChannels >= 6 &&
            copyMapping.SourceIndex < encodeMapping.SourceIndex)
        {
            mappings[0] = new EncodeAudioTrackMapping(
                encodeMapping.Title,
                encodeMapping.SourceStream,
                encodeMapping.SourceIndex,
                copyMapping.DestinationIndex,
                encodeMapping.DestinationCodec,
                encodeMapping.DestinationBitrate,
                encodeMapping.DestinationChannels);

            mappings[1] = new CopyAudioTrackMapping(
                copyMapping.Title,
                copyMapping.SourceStream,
                copyMapping.SourceIndex,
                encodeMapping.DestinationIndex);
        }

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

    private static bool IsDtsMaOrTrueHd(MediaStream stream)
    {
        var codec = stream.Codec?.Trim() ?? string.Empty;
        if (string.Equals(codec, "truehd", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(codec, "dts", StringComparison.OrdinalIgnoreCase))
            return false;

        var profile = stream.Profile?.Trim() ?? string.Empty;
        if (profile.Contains("ma", StringComparison.OrdinalIgnoreCase))
            return true;

        var codecLongName = stream.CodecLongName?.Trim() ?? string.Empty;
        return codecLongName.Contains("master audio", StringComparison.OrdinalIgnoreCase);
    }

    private static int NormalizeChannelCount(int channels)
    {
        if (channels <= 1)
            return 1;

        return channels;
    }

    private static int GetAacBitrate(int channels)
    {
        if (channels >= 8)
            return 512;
        if (channels >= 6)
            return 384;
        if (channels >= 2)
            return 160;

        return 80;
    }
}
