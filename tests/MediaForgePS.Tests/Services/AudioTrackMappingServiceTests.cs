using System;
using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class AudioTrackMappingServiceTests
{
    private readonly Mock<ILogger<AudioTrackMappingService>> _loggerMock;
    private readonly IAudioTrackMappingService _service;

    public AudioTrackMappingServiceTests()
    {
        _loggerMock = new Mock<ILogger<AudioTrackMappingService>>();
        _service = new AudioTrackMappingService(_loggerMock.Object);
    }

    private MediaStream CreateAudioStream(
        int index,
        string codec,
        string language,
        int channels,
        string? title = null,
        string profile = "",
        string codecLongName = "")
    {
        var tags = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(language))
            tags["language"] = language;
        if (!string.IsNullOrEmpty(title))
            tags["title"] = title;

        var rawJson = $@"{{
            ""index"": {index},
            ""codec_name"": ""{codec}"",
            ""codec_type"": ""audio"",
            ""channels"": {channels},
            ""tags"": {{
                {(language != null ? $@"""language"": ""{language}""," : "")}
                {(title != null ? $@"""title"": ""{title}""," : "")}
                ""DURATION-{language}"": ""00:43:29.500000""
            }}
        }}";

        return new MediaStream(
            "audio",
            index,
            codec,
            profile,
            codecLongName,
            tags,
            TimeSpan.Zero,
            language,
            rawJson);
    }

    [Fact]
    public void CreateMappings_WithNoEnglishAudioStreams_ReturnsEmptyArray()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(0, "aac", "spa", 2) // Spanish audio, not English
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CreateMappings_WithDtsStream_CreatesCopyMapping()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(1, "dts", "eng", 6, "DTS 5.1")
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Single(result);
        var mapping = Assert.IsType<CopyAudioTrackMapping>(result[0]);
        Assert.Equal(1, mapping.SourceIndex);
        Assert.Equal(0, mapping.DestinationIndex);
        Assert.Equal("DTS 5.1", mapping.Title);
    }

    [Fact]
    public void CreateMappings_WithSixChannelStream_CreatesAac384kbpsMapping()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(1, "ac3", "eng", 6, "5.1 Surround")
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Single(result);
        var mapping = Assert.IsType<EncodeAudioTrackMapping>(result[0]);
        Assert.Equal(1, mapping.SourceIndex);
        Assert.Equal(0, mapping.DestinationIndex);
        Assert.Equal("aac", mapping.DestinationCodec);
        Assert.Equal(384, mapping.DestinationBitrate);
        Assert.Equal(6, mapping.DestinationChannels);
        Assert.Equal("5.1 Surround", mapping.Title);
    }

    [Fact]
    public void CreateMappings_WithTwoChannelStream_CreatesAac160kbpsMapping()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(1, "aac", "eng", 2, "Stereo")
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Single(result);
        var mapping = Assert.IsType<EncodeAudioTrackMapping>(result[0]);
        Assert.Equal(1, mapping.SourceIndex);
        Assert.Equal(0, mapping.DestinationIndex);
        Assert.Equal("aac", mapping.DestinationCodec);
        Assert.Equal(160, mapping.DestinationBitrate);
        Assert.Equal(2, mapping.DestinationChannels);
        Assert.Equal("Stereo", mapping.Title);
    }

    [Fact]
    public void CreateMappings_WithOneChannelStream_CreatesAac80kbpsMapping()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(1, "aac", "eng", 1, "Mono")
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Single(result);
        var mapping = Assert.IsType<EncodeAudioTrackMapping>(result[0]);
        Assert.Equal(1, mapping.SourceIndex);
        Assert.Equal(0, mapping.DestinationIndex);
        Assert.Equal("aac", mapping.DestinationCodec);
        Assert.Equal(80, mapping.DestinationBitrate);
        Assert.Equal(1, mapping.DestinationChannels);
        Assert.Equal("Mono", mapping.Title);
    }

    [Fact]
    public void CreateMappings_WithDtsFirstAndSixChannelAacSecond_SwapsDestinationIndices()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 2, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(1, "dts", "eng", 6, "DTS 5.1"),
                CreateAudioStream(2, "aac", "eng", 6, "AAC 5.1")
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Equal(2, result.Length);
        var firstMapping = Assert.IsType<CopyAudioTrackMapping>(result[0]);
        var secondMapping = Assert.IsType<EncodeAudioTrackMapping>(result[1]);
        // After swap: DTS should have destination index 1, AAC should have destination index 0
        Assert.Equal(1, firstMapping.DestinationIndex);
        Assert.Equal(0, secondMapping.DestinationIndex);
    }

    [Fact]
    public void CreateMappings_WithMultipleStreams_CreatesSequentialDestinationIndices()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 3, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(1, "aac", "eng", 2, "Stereo"),
                CreateAudioStream(2, "aac", "eng", 1, "Mono"),
                CreateAudioStream(3, "aac", "eng", 6, "5.1")
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal(0, result[0].DestinationIndex);
        Assert.Equal(1, result[1].DestinationIndex);
        Assert.Equal(2, result[2].DestinationIndex);
    }

    [Fact]
    public void CreateMappings_WithMissingTitle_ReturnsNullTitle()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(1, "aac", "eng", 2) // No title
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Single(result);
        Assert.Null(result[0].Title);
    }

    [Fact]
    public void CreateMappings_WithCaseInsensitiveLanguage_FiltersEnglishStreams()
    {
        // Arrange
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 3, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                CreateAudioStream(1, "aac", "ENG", 2), // Uppercase
                CreateAudioStream(2, "aac", "eng", 2), // Lowercase
                CreateAudioStream(3, "aac", "EnG", 2)  // Mixed case
            },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void CreateMappings_WithMissingChannels_DefaultsToZero()
    {
        // Arrange
        var tags = new Dictionary<string, string> { ["language"] = "eng" };
        var streamWithoutChannels = new MediaStream(
            "audio",
            1,
            "aac",
            string.Empty,
            string.Empty,
            tags,
            TimeSpan.Zero,
            "eng",
            @"{""index"": 1, ""codec_name"": ""aac"", ""codec_type"": ""audio"", ""tags"": {""language"": ""eng""}}");

        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[] { streamWithoutChannels },
            "{}");

        // Act
        var result = _service.CreateMappings(mediaFile);

        // Assert
        Assert.Single(result);
        var mapping = Assert.IsType<EncodeAudioTrackMapping>(result[0]);
        // With 0 channels, it should default to 1 channel, 80 kbps
        Assert.Equal(80, mapping.DestinationBitrate);
        Assert.Equal(1, mapping.DestinationChannels);
    }

    [Fact]
    public void CreateMappings_WithNullMediaFile_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.CreateMappings(null!));
    }

    [Fact]
    public void CreateDirectoryEncodeMappings_WithDtsMaFirstAndSixChannelSecond_SwapsAndCopiesAsRequested()
    {
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 3, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                new MediaStream("video", 0, "h264", string.Empty, string.Empty, new Dictionary<string, string>(), TimeSpan.Zero, null, @"{""index"":0,""codec_type"":""video""}"),
                CreateAudioStream(1, "dts", "eng", 8, "DTS-HD MA", profile: "DTS-HD MA", codecLongName: "DTS-HD Master Audio"),
                CreateAudioStream(2, "ac3", "eng", 6, "AC3 5.1"),
                CreateAudioStream(3, "aac", "eng", 2, "Commentary")
            },
            "{}");

        var result = _service.CreateDirectoryEncodeMappings(mediaFile);

        Assert.Equal(3, result.Length);

        var first = Assert.IsType<EncodeAudioTrackMapping>(result[0]);
        Assert.Equal(1, first.SourceIndex);
        Assert.Equal(0, first.DestinationIndex);
        Assert.Equal("aac", first.DestinationCodec);
        Assert.Equal(6, first.DestinationChannels);

        var second = Assert.IsType<CopyAudioTrackMapping>(result[1]);
        Assert.Equal(0, second.SourceIndex);
        Assert.Equal(1, second.DestinationIndex);

        var third = Assert.IsType<EncodeAudioTrackMapping>(result[2]);
        Assert.Equal(2, third.SourceIndex);
        Assert.Equal(2, third.DestinationIndex);
        Assert.Equal(2, third.DestinationChannels);
    }

    [Fact]
    public void CreateDirectoryEncodeMappings_WithoutSwapCondition_EncodesAllEnglishTracksAsAac()
    {
        var mediaFile = new MediaFile(
            "C:\\test.mkv",
            new MediaFormat("C:\\test.mkv", 3, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            new[]
            {
                new MediaStream("video", 0, "h264", string.Empty, string.Empty, new Dictionary<string, string>(), TimeSpan.Zero, null, @"{""index"":0,""codec_type"":""video""}"),
                CreateAudioStream(1, "truehd", "eng", 8, "TrueHD 7.1"),
                CreateAudioStream(2, "aac", "eng", 2, "Stereo"),
                CreateAudioStream(3, "ac3", "eng", 6, "AC3 5.1")
            },
            "{}");

        var result = _service.CreateDirectoryEncodeMappings(mediaFile);

        Assert.Equal(3, result.Length);
        Assert.All(result, mapping => Assert.IsType<EncodeAudioTrackMapping>(mapping));

        var first = (EncodeAudioTrackMapping)result[0];
        var second = (EncodeAudioTrackMapping)result[1];
        var third = (EncodeAudioTrackMapping)result[2];

        Assert.Equal(8, first.DestinationChannels);
        Assert.Equal(2, second.DestinationChannels);
        Assert.Equal(6, third.DestinationChannels);
    }
}
