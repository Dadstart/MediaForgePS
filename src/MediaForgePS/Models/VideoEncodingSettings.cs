using System.Management.Automation;
using Microsoft.PowerShell.Commands;

namespace Dadstart.Labs.MediaForge.Models;

public record VideoEncodingSettings(
    string Codec,
    decimal? Bitrate,
    int? CRF,
    string Preset,
    string CodecProfile,
    string Tune)
{
    public override string ToString() => Bitrate.HasValue
        ? $"{Codec} {Bitrate.Value}k, Preset {Preset}"
        : $"{Codec} CRF {CRF}, Preset {Preset}";

    public IEnumerable<string> ToFfmpegArgs(int? pass)
    {
        if (!Bitrate.HasValue && !CRF.HasValue)
            throw new InvalidOperationException("Encoding must either specify bitrate or CRF");

        if (pass.HasValue && pass != 1 && pass != 2)
            throw new InvalidOperationException($"Invalid pass value {pass}");

        List<string> args = new();

        // Construct ffmpeg command
        if (CRF.HasValue || (pass.HasValue && pass == 2))
        {
            args.Add("-map");
            args.Add("0:v:0");
        }

        args.Add("-c:v");
        args.Add($Codec == "x264" ? "libx264" : Codec);
        args.Add("-preset");
        args.Add($Preset);
        if (Bitrate.HasValue) {
            args.Add("-b:v");
            args.Add($"{Bitrate.Value}");
        }
        else if (CRF.HasValue) {
            args.Add("-crf");
            args.Add(CRF.Value.ToString());
            args.Add("-pix_fmt");
            args.Add("yuv420p");
        }

        if (CRF.HasValue || (pass == 2)) {
            args.Add("-map_metadata");
            args.Add("0");
            args.Add("-map_chapters");
            args.Add("0");
            args.Add("-movflags");
            args.Add("+faststart");
        }

        return args;
    }
}
