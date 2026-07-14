using System.Text.Json.Serialization;

namespace Dadstart.Labs.MediaForge.Services.TvDb;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TvDbLoginRequest))]
[JsonSerializable(typeof(TvDbLoginResponse))]
[JsonSerializable(typeof(TvDbSeriesResponse))]
[JsonSerializable(typeof(TvDbSeriesEpisodesResponse))]
internal partial class TvDbJsonContext : JsonSerializerContext;
