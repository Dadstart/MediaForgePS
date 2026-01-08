using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Service for performing media file conversions with progress reporting.
/// </summary>
public interface IMediaConversionService
{
    /// <summary>
    /// Builds Ffmpeg arguments from video encoding settings and audio track mappings.
    /// </summary>
    /// <param name="videoSettings">Video encoding settings.</param>
    /// <param name="audioMappings">Audio track mappings.</param>
    /// <param name="pass">The encoding pass number (1 or 2 for two-pass, null for single-pass).</param>
    /// <param name="additionalArguments">Optional additional Ffmpeg arguments.</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    IEnumerable<string> BuildFfmpegArguments(
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        int? pass = null,
        string[]? additionalArguments = null);

    /// <summary>
    /// Executes a media file conversion with progress reporting.
    /// </summary>
    /// <param name="resolvedInputPath">Resolved input file path.</param>
    /// <param name="resolvedOutputPath">Resolved output file path.</param>
    /// <param name="videoSettings">Video encoding settings.</param>
    /// <param name="audioMappings">Audio track mappings.</param>
    /// <param name="progressCallback">Callback for reporting progress.</param>
    /// <param name="additionalArguments">Optional additional Ffmpeg arguments.</param>
    /// <returns>True if conversion succeeded.</returns>
    /// <exception cref="FfmpegConversionException">Thrown when FFmpeg conversion fails.</exception>
    bool ExecuteConversion(
        string resolvedInputPath,
        string resolvedOutputPath,
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        Action<FfmpegProgress, long?, string> progressCallback,
        string[]? additionalArguments = null);
}
