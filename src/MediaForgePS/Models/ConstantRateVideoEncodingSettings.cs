namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Video encoding settings for constant rate factor (CRF) encoding using single-pass encoding.
/// </summary>
public record ConstantRateVideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    int CRF)
    : VideoEncodingSettings(Codec, Preset, CodecProfile, Tune)
{
    /// <summary>
    /// Returns a string representation of the encoding settings.
    /// </summary>
    public override string ToString() => $"{Codec} CRF {CRF}, Preset {Preset}";

    /// <summary>
    /// Indicates that constant rate encoding uses single-pass encoding.
    /// </summary>
    public override bool IsSinglePass => true;

    /// <summary>
    /// Converts the encoding settings to a list of Ffmpeg arguments.
    /// </summary>
    /// <param name="pass">Ignored for constant rate encoding (always single-pass).</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public override IList<string> ToFfmpegArgs(int? pass)
    {
        List<string> args = new();

        AddVideoStreamMap(args);
        AddVideoCodec(args);
        AddPreset(args);

        args.Add("-crf");
        args.Add(CRF.ToString());

        args.Add("-pix_fmt");
        args.Add("yuv420p");

        AddMetadataChaptersAndMovflags(args);

        return args;
    }
}
