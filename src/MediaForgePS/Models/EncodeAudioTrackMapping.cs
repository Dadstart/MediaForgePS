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
    public override IList<string> ToFfmpegArgs(IPlatformService platformService)
    {
        ArgumentNullException.ThrowIfNull(platformService);

        var builder = new FfmpegArgumentBuilder(platformService);
        AddSourceMapArgs(builder);
        AddDestinationCodecArgs(builder, DestinationCodec);
        AddBitrateArgs(builder);
        AddChannelsArgs(builder);
        AddTitleMetadata(builder);
        return builder.ToArguments().ToList(); // REVIEW output type
    }

    private void AddChannelsArgs(FfmpegArgumentBuilder builder)
    {
        if (DestinationChannels > 0)
            builder.AddOption($"-ac:a:{DestinationIndex}", DestinationChannels.ToString());
    }

    private void AddBitrateArgs(FfmpegArgumentBuilder builder)
    {
        int bps;
        if (DestinationBitrate != 0)
        {
            bps = DestinationBitrate;
        }
        else
        {
            bps = DestinationChannels switch
            {
                1 => DefaultBitrateMono,
                2 => DefaultBitrateStereo,
                6 => DefaultBitrate5_1,
                8 => DefaultBitrate7_1,
                _ => throw new ArgumentOutOfRangeException(nameof(DestinationChannels), DestinationChannels, $"No default bitrate found for {DestinationChannels} channels")
            };
        }

        builder.AddOption($"-b:a:{DestinationIndex}", $"{bps}k");
    }

    /// <summary>
    /// Returns a string representation of the audio track mapping.
    /// </summary>
    public override string ToString() => $"Audio stream {SourceIndex} → {DestinationCodec}@{DestinationBitrate}k (→ index {DestinationIndex})";
}
