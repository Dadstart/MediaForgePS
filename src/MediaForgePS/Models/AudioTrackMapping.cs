using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

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
    /// Adds title metadata arguments to the Ffmpeg argument builder if a title is specified.
    /// </summary>
    /// <param name="builder">The Ffmpeg argument builder to append to.</param>
    protected void AddTitleMetadata(FfmpegArgumentBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(Title))
            builder.AddOption($"-metadata:s:a:{DestinationIndex}", $"title=\"{Title}\"");
    }

    /// <summary>
    /// Adds source stream mapping arguments to the Ffmpeg argument builder.
    /// </summary>
    /// <param name="builder">The Ffmpeg argument builder to append to.</param>
    protected void AddSourceMapArgs(FfmpegArgumentBuilder builder)
    {
        builder.AddOption("-map", $"{SourceStream}:a:{SourceIndex}");
    }

    /// <summary>
    /// Adds destination stream mapping and codec arguments to the Ffmpeg argument builder.
    /// </summary>
    /// <param name="builder">The Ffmpeg argument builder to append to.</param>
    /// <param name="codec">The codec to use for the destination stream (e.g., "copy" or "aac").</param>
    protected void AddDestinationCodecArgs(FfmpegArgumentBuilder builder, string codec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codec);
        builder.AddOption("-c:a", codec);
    }

    /// <summary>
    /// Converts the audio track mapping to a list of Ffmpeg arguments.
    /// </summary>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public abstract IList<string> ToFfmpegArgs(IPlatformService platformService);

    /// <summary>
    /// Returns a string representation of the audio track mapping.
    /// </summary>
    public abstract override string ToString();
}
