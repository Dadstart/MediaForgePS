namespace Dadstart.Labs.MediaForge.Models;

public record AudioTrackMapping(
    string Title,
    int SourceStream,
    int SourceIndex,
    int DestinationIndex,
    string DestinationCodec,
    int DestinationBitrate,
    int DestinationChannels,
    bool CopyOriginal)
{
    public override string ToString() => $"Audio stream {SourceIndex} → {DestinationCodec})@{DestinationBitrate}k (→ index {DestinationIndex}{(CopyOriginal ? " [Copy]" : string.Empty)}";

    public IEnumerable<string> ToFfmpegArgs()
    {
        List<string> args = new();
        args.Add("-map");
        args.Add($"{SourceStream}:a:{SourceIndex}");
        args.Add("-c:a:$($this.DestinationIndex)");
        if (CopyOriginal)
        {
            args.Add("copy");
            if ($this.Title) {
                args.Add($"-metadata:s:a:{DestinationIndex}")
                args.Add($"title=\"{Title}\"");
            }
        }
        else
        {
            args.Add(DestinationCodec);
            args.Add($"-b:a:{DestinationIndex}");
            if ((DestinationBitrate == 0) && (DestinationChannels == 0))
                throw new InvalidOperationException("No channels or bitrate provided for this audio track");

            var bps = DestinationBitrate;
            if (bps == 0)
            {
                // use default bitrate based on channel count
                switch (DestinationChannels)
                {
                    case 1: bps = 80; break;
                    case 2: bps = 160; break;
                    case 6: bps = 384; break;
                    case 8: bps = 512; break;
                    default:
                        throw new InvalidOperationException($"No default bitrate found for {DestinationChannels}");
                }
            }
            args.Add($"{bps}k");
            if (DestinationChannels > 0)
            {
                args.Add($"-ac:a:{DestinationIndex}");
                args.Add(DestinationChannels.ToString());
            }
            if (!string.IsNullOrWhiteSpace(Title) {
                args.Add($"-metadata:s:a:{DestinationIndex}");
                args.Add($"title=\"{Title}\"");
            }
        }

        return args;
    }
}
