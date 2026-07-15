using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Video encoding settings for variable bitrate encoding using two-pass encoding.
/// </summary>
public record VariableRateVideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    int Bitrate,
    string PixelFormat)
    : VideoEncodingSettings(Codec, Preset, CodecProfile, Tune, PixelFormat)
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
    public override IEnumerable<string> ToFfmpegArgs(int? pass)
    {
        if (pass != 1 && pass != 2)
            throw new ArgumentOutOfRangeException(nameof(pass), pass, "Pass must be 1 or 2 for variable rate encoding");

        return ToFfmpegArgs(pass.Value, passLogFile: null);
    }

    /// <summary>
    /// Converts the encoding settings to a list of Ffmpeg arguments for the specified pass and pass logfile.
    /// </summary>
    /// <param name="pass">The encoding pass number (must be 1 or 2).</param>
    /// <param name="passLogFile">Shared pass logfile path (without FFmpeg-generated extension).</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public IEnumerable<string> ToFfmpegArgs(int pass, string? passLogFile)
    {
        if (pass != 1 && pass != 2)
            throw new ArgumentOutOfRangeException(nameof(pass), pass, "Pass must be 1 or 2 for variable rate encoding");

        var builder = new FfmpegArgumentBuilder();

        builder.AddSourceMap(0, StreamType, 0);

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
            .AddBitrate(StreamType, 0, Bitrate)
            .AddPixelFormat(PixelFormat);

        if (!string.IsNullOrWhiteSpace(passLogFile))
            builder.AddPass(pass, passLogFile);

        // Pass 1 is analysis only; skip audio encode/copy for speed and use a null muxer downstream.
        if (pass == 1)
            builder.AddFlag("-an");

        return builder.ToArguments();
    }
}
