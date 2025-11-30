namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Base class for audio track mapping configurations used to map and configure audio streams in Ffmpeg.
/// </summary>
public abstract record AudioTrackMapping(
    string? Title,
    int SourceStream,
    int SourceIndex,
    int DestinationIndex)
{
    /// <summary>
    /// Adds title metadata arguments to the Ffmpeg argument list if a title is specified.
    /// </summary>
    /// <param name="args">The list of Ffmpeg arguments to append to.</param>
    protected void AddTitleMetadata(IList<string> args)
    {
        if (!string.IsNullOrWhiteSpace(Title))
        {
            args.Add($"-metadata:s:a:{DestinationIndex}");
            args.Add($"title=\"{Title}\"");
        }
    }

    /// <summary>
    /// Adds source stream mapping arguments to the Ffmpeg argument list.
    /// </summary>
    /// <param name="args">The list of Ffmpeg arguments to append to.</param>
    protected void AddSourceMapArgs(IList<string> args)
    {
        args.Add("-map");
        args.Add($"{SourceStream}:a:{SourceIndex}");
    }

    /// <summary>
    /// Adds destination stream mapping and codec arguments to the Ffmpeg argument list.
    /// </summary>
    /// <param name="args">The list of Ffmpeg arguments to append to.</param>
    /// <param name="codec">The codec to use for the destination stream (e.g., "copy" or "aac").</param>
    protected void AddDestinationCodecArgs(IList<string> args, string codec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codec);

        args.Add("-c:a");
        args.Add(codec);
    }

    /// <summary>
    /// Converts the audio track mapping to a list of Ffmpeg arguments.
    /// </summary>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public abstract IList<string> ToFfmpegArgs();

    /// <summary>
    /// Returns a string representation of the audio track mapping.
    /// </summary>
    public abstract override string ToString();
}
