namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Audio track mapping that copies an audio stream without re-encoding.
/// </summary>
public record CopyAudioTrackMapping(
    string? Title,
    int SourceStream,
    int SourceIndex,
    int DestinationIndex,
    string? DestinationCodec)
    : AudioTrackMapping(Title, SourceStream, SourceIndex, DestinationIndex)
{
    /// <summary>
    /// Converts the audio track mapping to a list of Ffmpeg arguments.
    /// </summary>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public override IList<string> ToFfmpegArgs()
    {
        List<string> args = new();
        AddSourceMapArgs(args);
        AddDestinationCodecArgs(args, GetDestinationCodec());
        AddTitleMetadata(args);
        return args;
    }

    private string GetDestinationCodec()
    {
        return string.IsNullOrWhiteSpace(DestinationCodec)
            ? "copy"
            : DestinationCodec!;
    }

    /// <summary>
    /// Returns a string representation of the audio track mapping.
    /// </summary>
    public override string ToString()
    {
        var codec = GetDestinationCodec();
        return $"Audio stream {SourceIndex} → {codec} (→ index {DestinationIndex}) [Copy]";
    }
}

