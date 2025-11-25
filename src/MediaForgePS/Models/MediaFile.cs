using System.Text.Json.Serialization;

namespace Dadstart.Labs.MediaForge.Models;

public record MediaFile(
    [property: JsonIgnore] string Path,
    [property: JsonPropertyName("format")] MediaFormat Format,
    [property: JsonPropertyName("chapters")] MediaChapter[] Chapters,
    [property: JsonPropertyName("streams")] MediaStream[] Streams,
    [property: JsonIgnore] string Raw
);
