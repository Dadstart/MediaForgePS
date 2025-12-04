using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Service interface for executing Ffprobe operations.
/// </summary>
public interface IFfprobeService
{
    /// <summary>
    /// Executes ffprobe to analyze a media file.
    /// </summary>
    /// <param name="path">Path to the media file to analyze.</param>
    /// <param name="arguments">Additional ffprobe arguments.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the ffprobe execution.</returns>
    Task<FfprobeResult> ExecuteAsync(string path, IEnumerable<string> arguments, CancellationToken cancellationToken = default);
}
