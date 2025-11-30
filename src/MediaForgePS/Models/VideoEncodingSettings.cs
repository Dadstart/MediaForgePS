namespace Dadstart.Labs.MediaForge.Models;

public abstract record VideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    IList<string> AdditionalArgs)
{
    public abstract bool IsSinglePass { get; }
    public abstract override string ToString();

    public abstract IList<string> ToFfmpegArgs(int? pass);
}
