using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Audio track mapping that copies an audio stream without re-encoding.
/// </summary>
public record CopyAudioTrackMapping(
    string? Title,
    int SourceStream,
    int SourceIndex,
    int DestinationIndex)
    : AudioTrackMapping(Title, SourceStream, SourceIndex, DestinationIndex)
{
    /// <summary>
    /// Converts the audio track mapping to a list of Ffmpeg arguments.
    /// </summary>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public override IEnumerable<string> ToFfmpegArgs(IPlatformService platformService)
    {
        ArgumentNullException.ThrowIfNull(platformService);

        var builder = new FfmpegArgumentBuilder(platformService);
        return builder
            .AddSourceMap(SourceStream, StreamType, SourceIndex)
            .AddDestinationCodec(StreamType, "copy")
            .AddTitleMetadata(StreamType, DestinationIndex, Title)
            .ToArguments();
    }

    /// <summary>
    /// Returns a string representation of the audio track mapping.
    /// </summary>
    public override string ToString() => $"Audio stream {SourceIndex} → copy (→ index {DestinationIndex}) [Copy]";
}

