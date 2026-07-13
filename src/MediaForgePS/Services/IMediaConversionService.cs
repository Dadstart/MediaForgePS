using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Service for performing media file conversions.
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
    /// Executes a media file conversion.
    /// </summary>
    /// <param name="resolvedInputPath">Resolved input file path.</param>
    /// <param name="resolvedOutputPath">Resolved output file path.</param>
    /// <param name="videoSettings">Video encoding settings.</param>
    /// <param name="audioMappings">Audio track mappings.</param>
    /// <param name="additionalArguments">Optional additional Ffmpeg arguments.</param>
    /// <param name="progress">Optional progress reporter for encode progress.</param>
    /// <param name="cancellationToken">Token used to cancel the conversion (and kill child processes).</param>
    /// <exception cref="FfmpegConversionException">Thrown when FFmpeg conversion fails.</exception>
    void ExecuteConversion(
        string resolvedInputPath,
        string resolvedOutputPath,
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        string[]? additionalArguments = null,
        IProgress<FfmpegProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
