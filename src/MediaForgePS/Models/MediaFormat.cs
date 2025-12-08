using System;
using System.Text.Json.Serialization;

namespace Dadstart.Labs.MediaForge.Models;

public record MediaFormat(
    [property: JsonIgnore] string? Title,
    [property: JsonPropertyName("filename")] string Path,
    [property: JsonPropertyName("nb_streams")] int StreamCount,
    [property: JsonPropertyName("format_name")] string Format,
    [property: JsonPropertyName("format_long_name")] string FormatLongName,
    [property: JsonPropertyName("start_time")] decimal StartTime,
    [property: JsonPropertyName("duration")] decimal Duration,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("bit_rate")] long BitRate,
    [property: JsonPropertyName("tags")] Dictionary<string, string> Tags,
    [property: JsonIgnore] string Raw
);
