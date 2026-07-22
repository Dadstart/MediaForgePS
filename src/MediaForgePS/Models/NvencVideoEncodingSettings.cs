using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Video encoding settings for NVENC hardware-accelerated encoding using constant quality (CQ) mode.
/// </summary>
public record NvencVideoEncodingSettings(
    string Preset,
    int Cq)
    : VideoEncodingSettings("hevc_nvenc", Preset, CodecProfile: null, Tune: null, "p010le")
{
    /// <summary>
    /// Returns a string representation of the encoding settings.
    /// </summary>
    public override string ToString() => $"{Codec} CQ {Cq}, Preset {Preset}";

    /// <summary>
    /// Indicates that NVENC constant quality encoding uses single-pass encoding.
    /// </summary>
    public override bool IsSinglePass => true;

    /// <summary>
    /// Converts the encoding settings to a list of Ffmpeg arguments.
    /// </summary>
    /// <param name="pass">Ignored for NVENC CQ encoding (always single-pass).</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public override IEnumerable<string> ToFfmpegArgs(int? pass)
    {
        var builder = new FfmpegArgumentBuilder();
        builder
            .AddSourceMap(0, StreamType, 0)
            .AddDestinationCodec(StreamType, Codec)
            .AddPreset(Preset)
            .AddProfile(CodecProfile)
            .AddTune(Tune)
            .AddCq(Cq)
            .AddPixelFormat(PixelFormat)
            .AddMapMetadata(0)
            .AddMapChapters(0)
            .AddMovFlags();

        return builder.ToArguments();
    }
}
