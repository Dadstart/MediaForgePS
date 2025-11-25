using System;
using System.Collections;
using System.Text.Json.Serialization;

namespace Dadstart.Labs.MediaForge.Models;

public record MediaStream(
    [property: JsonPropertyName("codec_type")] string Type,
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("codec_name")] string Codec,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("codec_long_name")] string CodecLongName,
    [property: JsonPropertyName("tags")] Dictionary<string, string> Tags,
    [property: JsonIgnore] TimeSpan Duration,
    [property: JsonIgnore] string? Language,
    [property: JsonIgnore] string Raw
);