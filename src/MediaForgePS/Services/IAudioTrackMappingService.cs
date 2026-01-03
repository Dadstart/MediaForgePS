using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Service interface for creating audio track mappings from media files.
/// </summary>
public interface IAudioTrackMappingService
{
    /// <summary>
    /// Creates audio track mappings for English audio streams in the media file.
    /// </summary>
    /// <param name="mediaFile">The media file to analyze.</param>
    /// <returns>An array of audio track mappings for English audio streams.</returns>
    AudioTrackMapping[] CreateMappings(MediaFile mediaFile);
}
