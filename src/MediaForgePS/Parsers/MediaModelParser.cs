using System;
using System.Collections;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Parsers;

public class MediaModelParser : IMediaModelParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    /// <inheritdoc />
    public MediaChapter ParseChapter(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        /*
        JSON example: {
            "id": 2751658996558931055,
            "time_base": "1/1000000000",
            "start": 0,
            "start_time": "0.000000",
            "end": 128128000000,
            "end_time": "128.128000",
            "tags": {
                "title": "Chapter 01"
            }
        }
        */

        var chapter = JsonSerializer.Deserialize<MediaChapter>(json, Options)
            ?? throw new JsonException("Failed to deserialize MediaChapter from JSON");

        return chapter with{ Title = chapter.Tags["title"], Raw = json };
    }

    /// <inheritdoc />
    public MediaFormat ParseFormat(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        /*
        JSON example:
        {
            "filename": "C:\\Videos\\my-video.mkv",
            "nb_streams": 5,
            "nb_programs": 0,
            "nb_stream_groups": 0,
            "format_name": "matroska,webm",
            "format_long_name": "Matroska / WebM",
            "start_time": "0.000000",
            "duration": "2609.481000",
            "size": "9611932320",
            "bit_rate": "29467721",
            "probe_score": 100,
            "tags": {
                "title": "My Great Video",
                "encoder": "libmakemkv v1.18.2 (1.3.10/1.5.2) win(x64-release)",
                "creation_time": "2025-11-14T22:39:55Z"
        }
        */

        var format = JsonSerializer.Deserialize<MediaFormat>(json, Options)
            ?? throw new JsonException("Failed to deserialize MediaFormat from JSON");

        return format with { Title = format.Tags["title"], Raw = json };
    }

    /// <inheritdoc />
    public MediaStream ParseStream(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        /*
        JSON example:
        {
            "index": 0,
            "codec_name": "h264",
            "codec_long_name": "H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10",
            "profile": "High",
            "codec_type": "video",
            // ... omitted for brevity
            "disposition": { ... },
            "tags": {
                "language": "eng",
                "BPS-eng": "24482213",
                "DURATION-eng": "00:43:29.481875000",
                "NUMBER_OF_FRAMES-eng": "62565",
                "NUMBER_OF_BYTES-eng": "7985733967",
                "SOURCE_ID-eng": "001011",
                // ... omitted for brevity
            }
        }
        */

        var stream = JsonSerializer.Deserialize<MediaStream>(json, Options)
            ?? throw new JsonException("Failed to deserialize MediaStream from JSON");

        var language = stream.Tags["language"];
        TimeSpan duration = TimeSpan.Zero;

        if (language is not null)
        {
            duration = TimeSpan.Parse(stream.Tags[$"DURATION-{language}"]);
        }

        return stream with { Language = language, Duration = duration, Raw = json };
    }

    /// <inheritdoc />
    public MediaFile ParseFile(string path, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        /*
        JSON example:
            {
            "streams": [..],
            "chapters": [..]
            "format": { ... },
            }
        */

        var format = JsonSerializer.Deserialize<MediaFormat>(json, Options)
            ?? throw new JsonException("Failed to deserialize MediaFormat from JSON");

        var chapters = JsonSerializer.Deserialize<MediaChapter[]>(json, Options)
            ?? throw new JsonException("Failed to deserialize MediaChapter[] from JSON");

        var streams = JsonSerializer.Deserialize<MediaStream[]>(json, Options)
            ?? throw new JsonException("Failed to deserialize MediaStream[] from JSON");

        return new MediaFile(path, format, chapters, streams, json);
    }
}
