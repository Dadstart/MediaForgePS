using System.Numerics;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Video encoding settings for variable bitrate encoding using two-pass encoding.
/// </summary>
public record VariableRateVideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    int Bitrate)
    : VideoEncodingSettings(Codec, Preset, CodecProfile, Tune)
{
    /// <summary>
    /// Returns a string representation of the encoding settings.
    /// </summary>
    public override string ToString() => $"{Codec} {Bitrate}k, Preset {Preset}";

    /// <summary>
    /// Indicates that variable rate encoding uses two-pass encoding.
    /// </summary>
    public override bool IsSinglePass => false;

    /// <summary>
    /// Converts the encoding settings to a list of Ffmpeg arguments for the specified pass.
    /// </summary>
    /// <param name="pass">The encoding pass number (must be 1 or 2).</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public override IEnumerable<string> ToFfmpegArgs(IPlatformService platformService, int? pass)
    {
        ArgumentNullException.ThrowIfNull(platformService);
        if (pass != 1 && pass != 2)
            throw new ArgumentOutOfRangeException(nameof(pass), pass, "Pass must be 1 or 2 for variable rate encoding");

        var builder = new FfmpegArgumentBuilder(platformService, new ArgumentBuilder(platformService));

        if (pass == 2)
        {
            builder
                .AddMapMetadata(0)
                .AddMapChapters(0)
                .AddMovFlags();
        }

        builder
            .AddDestinationCodec(StreamType, FfmpegCodecName)
            .AddPreset(Preset)
            .AddBitrate(StreamType, 0, Bitrate);

        return builder.ToArguments();
    }
}
