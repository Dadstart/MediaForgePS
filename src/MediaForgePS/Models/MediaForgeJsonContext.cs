using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dadstart.Labs.MediaForge.Models;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(MediaChapter))]
[JsonSerializable(typeof(MediaFormat))]
[JsonSerializable(typeof(MediaStream))]
internal partial class MediaForgeJsonContext : JsonSerializerContext;
