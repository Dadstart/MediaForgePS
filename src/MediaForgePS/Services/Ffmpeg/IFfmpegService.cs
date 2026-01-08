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
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>True if conversion succeeded.</returns>
    /// <exception cref="FfmpegConversionException">Thrown when FFmpeg conversion fails.</exception>
    Task<bool> ConvertAsync(string inputPath, string outputPath, IEnumerable<string>? arguments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a media file from one format to another with progress reporting.
    /// </summary>
    /// <param name="inputPath">Path to the input media file.</param>
    /// <param name="outputPath">Path to the output media file.</param>
    /// <param name="arguments">Optional additional Ffmpeg arguments.</param>
    /// <param name="progressCallback">Callback invoked when progress information is available.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>True if conversion succeeded.</returns>
    /// <exception cref="FfmpegConversionException">Thrown when FFmpeg conversion fails.</exception>
    Task<bool> ConvertAsync(string inputPath, string outputPath, IEnumerable<string>? arguments, Action<FfmpegProgress> progressCallback, CancellationToken cancellationToken = default);
}
