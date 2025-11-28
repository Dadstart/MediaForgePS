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
    /// <returns>True if conversion succeeded, false otherwise.</returns>
    Task<bool> ConvertAsync(string inputPath, string outputPath, IEnumerable<string>? arguments = null);
}
