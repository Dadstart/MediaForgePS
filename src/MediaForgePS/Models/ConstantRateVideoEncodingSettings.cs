namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Video encoding settings for constant rate factor (CRF) encoding using single-pass encoding.
/// </summary>
public record ConstantRateVideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    int CRF,
    IList<string> AdditionalArgs)
    : VideoEncodingSettings(Codec, Preset, CodecProfile, Tune, AdditionalArgs)
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

        args.Add("-map");
        args.Add("0:v:0");

        args.Add("-c:v");
        args.Add(Codec == "x264" ? "libx264" : Codec);
        args.Add("-preset");
        args.Add(Preset);

        args.Add("-crf");
        args.Add(CRF.ToString());

        args.Add("-pix_fmt");
        args.Add("yuv420p");

        args.Add("-map_metadata");
        args.Add("0");
        args.Add("-map_chapters");
        args.Add("0");
        args.Add("-movflags");
        args.Add("+faststart");

        if (AdditionalArgs != null)
            args.AddRange(AdditionalArgs);

        return args;
    }
}
