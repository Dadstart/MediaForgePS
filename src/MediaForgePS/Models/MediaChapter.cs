using System;
using System.Collections;
using System.Text.Json.Serialization;

namespace Dadstart.Labs.MediaForge.Models;

public record MediaChapter(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("start_time")] decimal StartTime,
    [property: JsonPropertyName("end_time")] decimal EndTime,
    [property: JsonPropertyName("tags")] Dictionary<string, string> Tags,
    [property: JsonIgnore] string? Title,
    [property: JsonIgnore] string Raw
)
    ;