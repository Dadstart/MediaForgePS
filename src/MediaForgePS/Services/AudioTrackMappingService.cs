using System;
using System.Collections.Generic;
using System.Linq;
using Dadstart.Labs.MediaForge.Models;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Creates audio track mappings from media file streams for conversion cmdlets.
/// </summary>
/// <remarks>
/// <see cref="CreateMappings"/> targets English audio only: DTS is copied; other codecs are AAC-encoded
/// with channel-based bitrates. <see cref="CreateDirectoryEncodeMappings"/> is used by
/// <see cref="Cmdlets.ConvertVideoFileCommand"/> and applies similar English-first rules.
/// <para>
/// <c>SourceIndex</c> values are 0-based ordinals among audio streams (for FFmpeg <c>-map 0:a:N</c>),
/// not ffprobe global stream indices.
/// </para>
/// </remarks>
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

        var audioIndexLookup = BuildAudioIndexLookup(mediaFile.Streams);

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
            int channels = stream.Channels;
            string? title = null;
            stream.Tags?.TryGetValue("title", out title);
            var sourceIndex = audioIndexLookup[stream.Index];

            AudioTrackMapping mapping;
            if (string.Equals(stream.Codec, "dts", StringComparison.OrdinalIgnoreCase))
            {
                // DTS: always copy
                mapping = new CopyAudioTrackMapping(
                    title,
                    0, // SourceStream: input file index (always 0 for single input)
                    sourceIndex,
                    destinationIndex);
            }
            else
            {
                // AAC encode bitrates by channel count: mono 80, stereo 160, 5.1+ 384 kbps
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
                    sourceIndex,
                    destinationIndex,
                    codec,
                    bitrate,
                    destChannels);
            }

            mappings.Add(mapping);
            destinationIndex++;
        }

        // When DTS copy and 6ch AAC encode would share destination order, swap indices so DTS is second
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
    /// <param name="selectedStreams">Selected streams to map.</param>
    /// <param name="allStreams">All streams from the media file (used to compute audio-relative indices).</param>
    /// <returns>Audio mappings for conversion workflows.</returns>
    public AudioTrackMapping[] CreateAutomaticMappings(
        IEnumerable<MediaStream> selectedStreams,
        IEnumerable<MediaStream> allStreams)
    {
        return CreateAutomaticMappingsFromStreams(selectedStreams, allStreams);
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

        var audioIndexLookup = BuildAudioIndexLookup(mediaFile.Streams);

        var mappings = new List<AudioTrackMapping>();
        var destinationIndex = 0;

        var usePreferredSwap = englishAudioStreams.Count >= 2 &&
            IsDtsMaOrTrueHd(englishAudioStreams[0]) &&
            englishAudioStreams[1].Channels == 6;

        if (usePreferredSwap)
        {
            var secondStream = englishAudioStreams[1];
            var secondChannels = NormalizeChannelCount(secondStream.Channels);
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
            var firstChannels = NormalizeChannelCount(firstStream.Channels);
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
                var secondChannels = NormalizeChannelCount(secondStream.Channels);
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
            var channels = NormalizeChannelCount(stream.Channels);
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

    /// <summary>
    /// Creates automatic audio mappings from selected streams.
    /// </summary>
    /// <param name="selectedStreams">Selected audio streams to map.</param>
    /// <param name="allStreams">All streams from the media file; used for <c>-map 0:a:N</c> ordinals.</param>
    public static AudioTrackMapping[] CreateAutomaticMappingsFromStreams(
        IEnumerable<MediaStream> selectedStreams,
        IEnumerable<MediaStream> allStreams)
    {
        ArgumentNullException.ThrowIfNull(selectedStreams);
        ArgumentNullException.ThrowIfNull(allStreams);

        var audioIndexLookup = BuildAudioIndexLookup(allStreams);
        var mappings = new List<AudioTrackMapping>();
        var destinationIndex = 0;

        foreach (var stream in selectedStreams)
        {
            if (!audioIndexLookup.TryGetValue(stream.Index, out var sourceIndex))
                throw new ArgumentException(
                    $"Stream index {stream.Index} was not found among audio streams in the media file.",
                    nameof(selectedStreams));

            var channels = NormalizeChannelCount(stream.Channels);
            stream.Tags.TryGetValue("title", out var title);

            AudioTrackMapping mapping;
            var codecLower = stream.Codec.ToLowerInvariant();
            if ((codecLower == "dts" || codecLower == "truehd") &&
                channels >= 6 &&
                !string.Equals(stream.Profile, "dts", StringComparison.OrdinalIgnoreCase))
            {
                mapping = new CopyAudioTrackMapping(title, 0, sourceIndex, destinationIndex);
            }
            else
            {
                mapping = new EncodeAudioTrackMapping(
                    title,
                    0,
                    sourceIndex,
                    destinationIndex,
                    "aac",
                    GetAacBitrate(channels),
                    channels);
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
    /// Builds a map from ffprobe global stream index to 0-based audio ordinal (FFmpeg <c>-map 0:a:N</c>).
    /// </summary>
    internal static Dictionary<int, int> BuildAudioIndexLookup(IEnumerable<MediaStream> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);

        return streams
            .Where(s => string.Equals(s.Type, "audio", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Index)
            .Select((stream, index) => new { stream.Index, AudioIndex = index })
            .ToDictionary(entry => entry.Index, entry => entry.AudioIndex);
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
