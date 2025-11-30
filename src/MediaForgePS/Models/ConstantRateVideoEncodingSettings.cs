using System.Management.Automation;
using Microsoft.PowerShell.Commands;

namespace Dadstart.Labs.MediaForge.Models;

public record ConstantRateVideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    int CRF,
    IList<string> AdditionalArgs)
    : VideoEncodingSettings(Codec, Preset, CodecProfile, Tune, AdditionalArgs)
{
    public override string ToString() => $"{Codec} CRF {CRF}, Preset {Preset}";

    public override bool IsSinglePass => true;

    /// <summary>
    /// Converts the encoding settings to a list of Ffmpeg arguments.
    /// </summary>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public override IList<string> ToFfmpegArgs(int? pass)
    {
        List<string> args = new();

        // Construct ffmpeg command
        // copy video stream
        args.Add("-map");
        args.Add("0:v:0");

        // set codec
        args.Add("-c:v");
        args.Add(Codec == "x264" ? "libx264" : Codec);
        args.Add("-preset");
        args.Add(Preset);

        // set constant rate
        args.Add("-crf");
        args.Add(CRF.ToString());

        // set pixel format
        args.Add("-pix_fmt");
        args.Add("yuv420p");

        // copy metadata and chapters
        args.Add("-map_metadata");
        args.Add("0");
        args.Add("-map_chapters");
        args.Add("0");
        args.Add("-movflags");

        // optimize for streaming
        args.Add("+faststart");

        args.AddRange(AdditionalArgs);
        return args;
    }
}
