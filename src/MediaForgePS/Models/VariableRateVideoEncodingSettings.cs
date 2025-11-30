using System.Management.Automation;
using Microsoft.PowerShell.Commands;

namespace Dadstart.Labs.MediaForge.Models;

public record VariableRateVideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    decimal Bitrate,
    IList<string> AdditionalArgs)
    : VideoEncodingSettings(Codec, Preset, CodecProfile, Tune, AdditionalArgs)
{
    public override string ToString() => $"{Codec} {Bitrate}k, Preset {Preset}";

    public override bool IsSinglePass => false;

    public override IList<string> ToFfmpegArgs(int? pass)
    {
        List<string> args = new();

        if (pass != 1 && pass != 2)
            throw new InvalidOperationException($"Invalid pass value {pass}");

        // Construct ffmpeg command
        if (pass == 2)
        {
            args.Add("-map");
            args.Add("0:v:0");
        }

        args.Add("-c:v");
        args.Add(Codec == "x264" ? "libx264" : Codec);
        args.Add("-preset");
        args.Add(Preset);
        args.Add("-b:v");
        args.Add(Bitrate.ToString());

        if (pass == 2)
        {
            args.Add("-map_metadata");
            args.Add("0");
            args.Add("-map_chapters");
            args.Add("0");
            args.Add("-movflags");
            args.Add("+faststart");
        }

        args.AddRange(AdditionalArgs);
        return args;
    }
}
