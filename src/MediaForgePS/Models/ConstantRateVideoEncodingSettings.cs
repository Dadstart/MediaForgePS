using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

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
    public override IList<string> ToFfmpegArgs(IPlatformService platformService, int? pass)
    {
        ArgumentNullException.ThrowIfNull(platformService);
        var builder = new FfmpegArgumentBuilder(platformService);

        AddVideoStreamMap(builder);
        AddVideoCodec(builder);
        AddPreset(builder);
        builder.AddOption("-crf", CRF.ToString());
        builder.AddOption("-pix_fmt", "yuv420p");
        AddMetadataChaptersAndMovflags(builder);

        return builder.ToArguments().ToList(); // REVIEW output type
    }
}
