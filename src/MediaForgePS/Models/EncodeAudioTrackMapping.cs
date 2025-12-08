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
    /// <summary>
    /// Default bitrate for mono audio tracks (80 kbps).
    /// Based on common AAC codec recommendations for mono audio encoding.
    /// </summary>
    private const int DefaultBitrateMono = 80;

    /// <summary>
    /// Default bitrate for stereo audio tracks (160 kbps).
    /// Based on common AAC codec recommendations for stereo audio encoding.
    /// </summary>
    private const int DefaultBitrateStereo = 160;

    /// <summary>
    /// Default bitrate for 5.1 surround sound audio tracks (384 kbps).
    /// Based on common AAC codec recommendations for 5.1 channel audio encoding.
    /// </summary>
    private const int DefaultBitrate5_1 = 384;

    /// <summary>
    /// Default bitrate for 7.1 surround sound audio tracks (512 kbps).
    /// Based on common AAC codec recommendations for 7.1 channel audio encoding.
    /// </summary>
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
