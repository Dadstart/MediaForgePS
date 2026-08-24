using System.Text.Json.Serialization;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Media file metadata returned by <see cref="Cmdlets.GetMediaFileCommand"/>.
/// </summary>
/// <param name="Path">Resolved file path on disk.</param>
/// <param name="Format">Container format metadata from ffprobe.</param>
/// <param name="Chapters">Chapter markers, if present.</param>
/// <param name="Streams">Video, audio, subtitle, and other streams.</param>
public record MediaFile(
    [property: JsonIgnore] string Path,
    [property: JsonPropertyName("format")] MediaFormat Format,
    [property: JsonPropertyName("chapters")] MediaChapter[] Chapters,
    [property: JsonPropertyName("streams")] MediaStream[] Streams
);
