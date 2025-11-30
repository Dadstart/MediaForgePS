namespace Dadstart.Labs.MediaForge.Models;

public record CopyAudioTrackMapping(
    string? Title,
    int SourceStream,
    int SourceIndex,
    int DestinationIndex,
    string? DestinationCodec)
    : AudioTrackMapping(Title, SourceStream, SourceIndex, DestinationIndex)
{
    public override IList<string> ToFfmpegArgs()
    {
        List<string> args = new();
        AddSourceMapArgs(args);
        AddDestinationMapArgs(args, "copy");
        AddTitleMetadata(args);
        return args;
    }

    public override string ToString() => $"Audio stream {SourceIndex} → copy (→ index {DestinationIndex}) [Copy]";
}

