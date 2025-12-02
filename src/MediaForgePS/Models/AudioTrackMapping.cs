using System.Runtime.CompilerServices;
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
    public const char StreamType = 'a';

    /// <summary>
    /// Converts the audio track mapping to a list of Ffmpeg arguments.
    /// </summary>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public abstract IEnumerable<string> ToFfmpegArgs(IPlatformService platformService);

    /// <summary>
    /// Returns a string representation of the audio track mapping.
    /// </summary>
    public abstract override string ToString();
}
