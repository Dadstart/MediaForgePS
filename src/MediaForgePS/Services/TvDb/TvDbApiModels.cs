using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Dadstart.Labs.MediaForge.Services.TvDb;

internal sealed class TvDbLoginRequest
{
    [JsonPropertyName("apikey")]
    public required string ApiKey { get; init; }

    [JsonPropertyName("pin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pin { get; init; }
}

internal sealed class TvDbLoginResponse
{
    [JsonPropertyName("data")]
    public TvDbLoginData? Data { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

internal sealed class TvDbLoginData
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }
}

internal sealed class TvDbSeriesResponse
{
    [JsonPropertyName("data")]
    public TvDbSeriesData? Data { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

internal sealed class TvDbSeriesData
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }
}

internal sealed class TvDbSeriesEpisodesResponse
{
    [JsonPropertyName("data")]
    public TvDbSeriesEpisodesData? Data { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("links")]
    public TvDbLinks? Links { get; init; }
}

internal sealed class TvDbSeriesEpisodesData
{
    [JsonPropertyName("episodes")]
    public IReadOnlyList<TvDbEpisodeData>? Episodes { get; init; }
}

internal sealed class TvDbEpisodeData
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("number")]
    public int? Number { get; init; }

    [JsonPropertyName("seasonNumber")]
    public int? SeasonNumber { get; init; }
}

internal sealed class TvDbLinks
{
    [JsonPropertyName("next")]
    public string? Next { get; init; }

    [JsonPropertyName("prev")]
    public string? Prev { get; init; }

    [JsonPropertyName("self")]
    public string? Self { get; init; }
}
