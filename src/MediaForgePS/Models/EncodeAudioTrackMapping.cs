namespace Dadstart.Labs.MediaForge.Models;

public record EncodeAudioTrackMapping(
    string? Title,
    int SourceStream,
    int SourceIndex,
    int DestinationIndex,
    string DestinationCodec,
    int DestinationBitrate,
    int DestinationChannels)
    : AudioTrackMapping(Title, SourceStream, SourceIndex, DestinationIndex)
{
    public override IList<string> ToFfmpegArgs()
    {
        List<string> args = new();
        AddSourceMapArgs(args);
        AddDestinationMapArgs(args, DestinationCodec);
        AddBitrateArgs(args);
        AddChannelsArgs(args);
        AddTitleMetadata(args);
        return args;
    }

    private void AddChannelsArgs(IList<string> args)
    {
        if (DestinationChannels > 0)
        {
            args.Add($"-ac:a:{DestinationIndex}");
            args.Add(DestinationChannels.ToString());
        }
    }

    private void AddBitrateArgs(IList<string> args)
    {
        int bps;
        if (DestinationBitrate != 0)
        {
            bps = DestinationBitrate;
        }
        else
        {
            bps = DestinationChannels switch
            {
                1 => 80,
                2 => 160,
                6 => 384,
                8 => 512,
                _ => throw new InvalidOperationException($"No default bitrate found for {DestinationChannels} channels")
            };
        }

        args.Add($"-b:a:{DestinationIndex}");
        args.Add($"{bps}k");
    }

    public override string ToString() => $"Audio stream {SourceIndex} → {DestinationCodec}@{DestinationBitrate}k (→ index {DestinationIndex})";
}

