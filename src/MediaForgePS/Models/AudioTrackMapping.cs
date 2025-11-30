namespace Dadstart.Labs.MediaForge.Models;

public abstract record AudioTrackMapping(
    string? Title,
    int SourceStream,
    int SourceIndex,
    int DestinationIndex)
{
    protected void AddTitleMetadata(IList<string> args)
    {
        if (!string.IsNullOrWhiteSpace(Title))
        {
            args.Add($"-metadata:s:a:{DestinationIndex}");
            args.Add($"title=\"{Title}\"");
        }
    }

    protected void AddSourceMapArgs(IList<string> args)
    {
        args.Add("-map");
        args.Add($"{SourceStream}:a:{SourceIndex}");
    }

    protected void AddDestinationMapArgs(IList<string> args, string codec)
    {
        args.Add("-map");
        args.Add($"{SourceStream}:a:{SourceIndex}");
        args.Add(codec);
    }

    public abstract IList<string> ToFfmpegArgs();

    public abstract override string ToString();
}
