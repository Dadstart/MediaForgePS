using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Service interface for reading media file information.
/// </summary>
public interface IMediaReaderService
{
    /// <summary>
    /// Retrieves media file information by analyzing the file with ffprobe.
    /// </summary>
    /// <param name="path">Path to the media file to analyze.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The media file information, or null if the file could not be analyzed.</returns>
    Task<MediaFile?> GetMediaFileAsync(string path, CancellationToken cancellationToken = default);
}
