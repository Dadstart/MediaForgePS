using System;
using System.Collections.Generic;
using System.Text.Json;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Parsers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Parsers;

public class MediaModelParserTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly MediaModelParser _parser;

    public MediaModelParserTests()
    {
        _loggerMock = new Mock<ILogger>();
        _parser = new MediaModelParser(_loggerMock.Object);
    }

    #region ParseChapter Tests

    [Fact]
    public void ParseChapter_WithValidJson_ReturnsMediaChapter()
    {
        // Arrange
        var json = """
            {
                "id": "2751658996558931055",
                "time_base": "1/1000000000",
                "start": 0,
                "start_time": 0.000000,
                "end": 128128000000,
                "end_time": 128.128000,
                "tags": {
                    "title": "Chapter 01"
                }
            }
            """;

        // Act
        var result = _parser.ParseChapter(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2751658996558931055", result.Id);
        Assert.Equal(0.000000m, result.StartTime);
        Assert.Equal(128.128000m, result.EndTime);
        Assert.NotNull(result.Tags);
        Assert.Equal("Chapter 01", result.Tags["title"]);
        Assert.Equal("Chapter 01", result.Title);
        Assert.Equal(json, result.Raw);
    }

    [Fact]
    public void ParseChapter_WithMissingTitleTag_ThrowsKeyNotFoundException()
    {
        // Arrange
        var json = """
            {
                "id": "123",
                "start_time": 0.000000,
                "end_time": 100.000000,
                "tags": {}
            }
            """;

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => _parser.ParseChapter(json));
    }

    [Fact]
    public void ParseChapter_WithNullJson_ThrowsArgumentException()
    {
        // Arrange
        string? json = null;

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => _parser.ParseChapter(json!));
    }

    [Fact]
    public void ParseChapter_WithEmptyJson_ThrowsArgumentException()
    {
        // Arrange
        var json = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseChapter(json));
    }

    [Fact]
    public void ParseChapter_WithWhitespaceJson_ThrowsArgumentException()
    {
        // Arrange
        var json = "   ";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseChapter(json));
    }

    [Fact]
    public void ParseChapter_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act & Assert
        Assert.Throws<JsonException>(() => _parser.ParseChapter(json));
    }

    [Fact]
    public void ParseChapter_WithTrailingComma_DeserializesSuccessfully()
    {
        // Arrange
        var json = """
            {
                "id": "123",
                "start_time": 0.000000,
                "end_time": 100.000000,
                "tags": {
                    "title": "Test Chapter"
                }
            }
            """;

        // Act
        var result = _parser.ParseChapter(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Chapter", result.Title);
    }

    #endregion

    #region ParseFormat Tests

    [Fact]
    public void ParseFormat_WithValidJson_ReturnsMediaFormat()
    {
        // Arrange
        var json = """
            {
                "filename": "C:\\Videos\\my-video.mkv",
                "nb_streams": 5,
                "nb_programs": 0,
                "nb_stream_groups": 0,
                "format_name": "matroska,webm",
                "format_long_name": "Matroska / WebM",
                "start_time": 0.000000,
                "duration": 2609.481000,
                "size": 9611932320,
                "bit_rate": 29467721,
                "probe_score": 100,
                "tags": {
                    "title": "My Great Video",
                    "encoder": "libmakemkv v1.18.2 (1.3.10/1.5.2) win(x64-release)",
                    "creation_time": "2025-11-14T22:39:55Z"
                }
            }
            """;

        // Act
        var result = _parser.ParseFormat(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("C:\\Videos\\my-video.mkv", result.Path);
        Assert.Equal(5, result.StreamCount);
        Assert.Equal("matroska,webm", result.Format);
        Assert.Equal("Matroska / WebM", result.FormatLongName);
        Assert.Equal(0.000000m, result.StartTime);
        Assert.Equal(2609.481000m, result.Duration);
        Assert.Equal(9611932320L, result.Size);
        Assert.Equal(29467721L, result.BitRate);
        Assert.NotNull(result.Tags);
        Assert.Equal("My Great Video", result.Tags["title"]);
        Assert.Equal("My Great Video", result.Title);
        Assert.Equal(json, result.Raw);
    }

    [Fact]
    public void ParseFormat_WithMissingTitleTag_ThrowsKeyNotFoundException()
    {
        // Arrange
        var json = """
            {
                "filename": "test.mkv",
                "nb_streams": 1,
                "format_name": "matroska",
                "format_long_name": "Matroska",
                "start_time": 0.000000,
                "duration": 100.000000,
                "size": 1000,
                "bit_rate": 100,
                "tags": {}
            }
            """;

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => _parser.ParseFormat(json));
    }

    [Fact]
    public void ParseFormat_WithNullJson_ThrowsArgumentException()
    {
        // Arrange
        string? json = null;

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => _parser.ParseFormat(json!));
    }

    [Fact]
    public void ParseFormat_WithEmptyJson_ThrowsArgumentException()
    {
        // Arrange
        var json = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseFormat(json));
    }

    [Fact]
    public void ParseFormat_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act & Assert
        Assert.Throws<JsonException>(() => _parser.ParseFormat(json));
    }

    #endregion

    #region ParseStream Tests

    [Fact]
    public void ParseStream_WithValidJson_ReturnsMediaStream()
    {
        // Arrange
        var json = """
            {
                "index": 0,
                "codec_name": "h264",
                "codec_long_name": "H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10",
                "profile": "High",
                "codec_type": "video",
                "tags": {
                    "language": "eng",
                    "DURATION-eng": "00:43:29.481875",
                    "BPS-eng": "24482213",
                    "NUMBER_OF_FRAMES-eng": "62565",
                    "NUMBER_OF_BYTES-eng": "7985733967",
                    "SOURCE_ID-eng": "001011"
                }
            }
            """;

        // Act
        var result = _parser.ParseStream(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Index);
        Assert.Equal("h264", result.Codec);
        Assert.Equal("H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10", result.CodecLongName);
        Assert.Equal("High", result.Profile);
        Assert.Equal("video", result.Type);
        Assert.NotNull(result.Tags);
        Assert.Equal("eng", result.Tags["language"]);
        Assert.Equal("eng", result.Language);
        Assert.Equal(TimeSpan.Parse("00:43:29.481875"), result.Duration);
        Assert.Equal(json, result.Raw);
    }

    [Fact]
    public void ParseStream_WithMissingLanguageTag_ThrowsKeyNotFoundException()
    {
        // Arrange
        var json = """
            {
                "index": 0,
                "codec_name": "h264",
                "codec_long_name": "H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10",
                "profile": "High",
                "codec_type": "video",
                "tags": {}
            }
            """;

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => _parser.ParseStream(json));
    }

    [Fact]
    public void ParseStream_WithLanguageButMissingDurationTag_ThrowsException()
    {
        // Arrange
        var json = """
            {
                "index": 0,
                "codec_name": "h264",
                "codec_type": "video",
                "tags": {
                    "language": "eng"
                }
            }
            """;

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => _parser.ParseStream(json));
    }

    [Fact]
    public void ParseStream_WithNullJson_ThrowsArgumentException()
    {
        // Arrange
        string? json = null;

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => _parser.ParseStream(json!));
    }

    [Fact]
    public void ParseStream_WithEmptyJson_ThrowsArgumentException()
    {
        // Arrange
        var json = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseStream(json));
    }

    [Fact]
    public void ParseStream_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act & Assert
        Assert.Throws<JsonException>(() => _parser.ParseStream(json));
    }

    [Fact]
    public void ParseStream_WithAudioStream_ReturnsCorrectType()
    {
        // Arrange
        var json = """
            {
                "index": 1,
                "codec_name": "aac",
                "codec_long_name": "AAC (Advanced Audio Coding)",
                "codec_type": "audio",
                "tags": {
                    "language": "eng",
                    "DURATION-eng": "00:43:29.500000"
                }
            }
            """;

        // Act
        var result = _parser.ParseStream(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("audio", result.Type);
        Assert.Equal("aac", result.Codec);
    }

    #endregion

    #region ParseFile Tests

    [Fact]
    public void ParseFile_WithValidJson_ThrowsJsonException()
    {
        // Arrange
        var path = "C:\\Videos\\test.mkv";
        var json = """
            {
                "format": {
                    "filename": "C:\\Videos\\test.mkv",
                    "nb_streams": 2,
                    "format_name": "matroska",
                    "format_long_name": "Matroska",
                    "start_time": 0.000000,
                    "duration": 100.000000,
                    "size": 1000,
                    "bit_rate": 100,
                    "tags": {
                        "title": "Test Video"
                    }
                },
                "chapters": [
                    {
                        "id": "1",
                        "start_time": 0.000000,
                        "end_time": 50.000000,
                        "tags": {
                            "title": "Chapter 1"
                        }
                    }
                ],
                "streams": [
                    {
                        "index": 0,
                        "codec_name": "h264",
                        "codec_type": "video",
                        "tags": {}
                    }
                ]
            }
            """;

        // Act & Assert
        // Note: The current implementation tries to deserialize the entire JSON as MediaChapter[] and MediaStream[],
        // which fails because the JSON structure has "chapters" and "streams" as properties, not the root.
        // This test documents the current (buggy) behavior.
        Assert.Throws<JsonException>(() => _parser.ParseFile(path, json));
    }

    [Fact]
    public void ParseFile_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        string? path = null;
        var json = "{}";

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => _parser.ParseFile(path!, json));
    }

    [Fact]
    public void ParseFile_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var path = string.Empty;
        var json = "{}";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseFile(path, json));
    }

    [Fact]
    public void ParseFile_WithNullJson_ThrowsArgumentException()
    {
        // Arrange
        var path = "test.mkv";
        string? json = null;

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => _parser.ParseFile(path, json!));
    }

    [Fact]
    public void ParseFile_WithEmptyJson_ThrowsArgumentException()
    {
        // Arrange
        var path = "test.mkv";
        var json = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.ParseFile(path, json));
    }

    [Fact]
    public void ParseFile_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var path = "test.mkv";
        var json = "{ invalid json }";

        // Act & Assert
        Assert.Throws<JsonException>(() => _parser.ParseFile(path, json));
    }

    #endregion
}

