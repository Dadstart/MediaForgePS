using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Service interface for executing Ffmpeg operations.
/// </summary>
public interface IFfmpegService
{
    /// <summary>
    /// Converts a media file from one format to another.
    /// </summary>
    /// <param name="inputPath">Path to the input media file.</param>
    /// <param name="outputPath">Path to the output media file.</param>
    /// <param name="arguments">Optional additional Ffmpeg arguments.</param>
    /// <param name="progress">Optional progress reporter for encode progress based on Ffmpeg <c>out_time</c>.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="timeout">
    /// Optional wall-clock timeout linked with <paramref name="cancellationToken"/>.
    /// Defaults to <see cref="ProcessTimeouts.Encode"/> when omitted.
    /// </param>
    /// <param name="overwrite">
    /// When false, refuses to replace an existing <paramref name="outputPath"/>.
    /// Pass true only after the caller has approved overwrite (e.g. -Force).
    /// </param>
    /// <exception cref="FfmpegConversionException">Thrown when FFmpeg conversion fails.</exception>
    /// <exception cref="IOException">Thrown when <paramref name="outputPath"/> exists and <paramref name="overwrite"/> is false.</exception>
    Task ConvertAsync(
        string inputPath,
        string outputPath,
        IEnumerable<string>? arguments = null,
        IProgress<FfmpegProgress>? progress = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        bool overwrite = false);
}
