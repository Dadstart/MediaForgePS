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
    public override IList<string> ToFfmpegArgs(int? pass)
    {
        if (pass != 1 && pass != 2)
            throw new ArgumentOutOfRangeException(nameof(pass), pass, "Pass must be 1 or 2 for variable rate encoding");

        List<string> args = new();

        if (pass == 2)
        {
            AddVideoStreamMap(args);
        }

        AddVideoCodec(args);
        AddPreset(args);
        args.Add("-b:v");
        args.Add($"{Bitrate}k");

        if (pass == 2)
        {
            AddMetadataChaptersAndMovflags(args);
        }

        return args;
    }
}
