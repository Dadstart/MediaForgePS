using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Base class for video encoding settings used to configure Ffmpeg video encoding parameters.
/// </summary>
public abstract record VideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    string PixelFormat)
{
    public const char StreamType = 'v';

    /// <summary>
    /// Indicates whether the encoding uses a single pass (true) or two-pass encoding (false).
    /// </summary>
    public abstract bool IsSinglePass { get; }

    /// <summary>
    /// Returns a string representation of the encoding settings.
    /// </summary>
    public abstract override string ToString();

    /// <summary>
    /// Converts the encoding settings to a list of Ffmpeg arguments for the specified pass.
    /// </summary>
    /// <param name="pass">The encoding pass number (1 or 2 for two-pass, null for single-pass).</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public abstract IEnumerable<string> ToFfmpegArgs(IPlatformService platformService, int? pass);

    /// <summary>
    /// Converts the codec name to the Ffmpeg codec name (e.g., "x264" to "libx264").
    /// </summary>
    /// <param name="codec">The codec name.</param>
    /// <returns>The Ffmpeg codec name.</returns>
    private static string ConvertToFfmpegCodec(string codec) => codec == "x264" ? "libx264" : codec;

    /// <summary>
    /// Converts the codec name to the Ffmpeg codec name (e.g., "x264" to "libx264").
    /// </summary>
    protected string FfmpegCodecName => ConvertToFfmpegCodec(Codec);

    /// <summary>
    /// Gets the default pixel format based on the codec.
    /// </summary>
    /// <param name="codec">The codec name.</param>
    /// <returns>The default pixel format for the codec.</returns>
    public static string GetDefaultPixelFormat(string codec)
    {
        var ffmpegCodec = ConvertToFfmpegCodec(codec);
        return ffmpegCodec == "libx265" || ffmpegCodec == "x265" ? "yuv420p10le" : "yuv420p";
    }
}
