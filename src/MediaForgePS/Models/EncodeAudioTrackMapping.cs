using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Audio track mapping that encodes an audio stream with specified codec, bitrate, and channel configuration.
/// </summary>
public record EncodeAudioTrackMapping(
    string? Title,
    int SourceStream,
    int SourceIndex,
    int DestinationIndex,
    string DestinationCodec,
    int DestinationBitrate,
    int DestinationChannels)
    : AudioTrackMapping(Title, SourceStream, SourceIndex, DestinationIndex)
{
    private const int DefaultBitrateMono = 80;
    private const int DefaultBitrateStereo = 160;
    private const int DefaultBitrate5_1 = 384;
    private const int DefaultBitrate7_1 = 512;

    /// <summary>
    /// Converts the audio track mapping to a list of Ffmpeg arguments.
    /// </summary>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public override IEnumerable<string> ToFfmpegArgs(IPlatformService platformService)
    {
        ArgumentNullException.ThrowIfNull(platformService);

        var builder = new FfmpegArgumentBuilder(platformService);
        return builder
            .AddSourceMap(SourceStream, StreamType, SourceIndex)
            .AddDestinationCodec(StreamType, DestinationCodec)
            .AddBitrate(StreamType, DestinationIndex, Bitrate)
            .AddAudioChannels(DestinationIndex, DestinationChannels)
            .AddTitleMetadata(StreamType, DestinationIndex, Title)
            .ToArguments();
    }

    /// <summary>
    /// Effective bitrate to use for encoding, either the specified value or a default based on channel count.
    /// </summary>
    private int Bitrate
    {
        get
        {
            // always use specified value
            if (DestinationBitrate != 0)
                return DestinationBitrate;

            // set based on channel count
            return DestinationChannels switch
            {
                1 => DefaultBitrateMono,
                2 => DefaultBitrateStereo,
                6 => DefaultBitrate5_1,
                8 => DefaultBitrate7_1,
                _ => throw new ArgumentOutOfRangeException(nameof(DestinationChannels), DestinationChannels, $"No default bitrate found for {DestinationChannels} channels")
            };
        }
    }


    /// <summary>
    /// Returns a string representation of the audio track mapping.
    /// </summary>
    public override string ToString() => $"Audio stream {SourceIndex} → {DestinationCodec}@{DestinationBitrate}k (→ index {DestinationIndex})";
}
