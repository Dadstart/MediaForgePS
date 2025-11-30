using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class EncodeAudioTrackMappingTests
{
    [Fact]
    public void ToFfmpegArgs_IncludesSourceMap()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 192, 2);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-map", args);
        Assert.Contains("0:a:1", args);
    }

    [Fact]
    public void ToFfmpegArgs_UsesDestinationCodec()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 192, 2);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-c:a", args);
        Assert.Contains("aac", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithSpecifiedBitrate_UsesSpecifiedBitrate()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 256, 2);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-b:a:2", args);
        Assert.Contains("256k", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithZeroBitrate_UsesDefaultBitrate()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 0, 2);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-b:a:2", args);
        Assert.Contains("160k", args); // Default for stereo
    }

    [Fact]
    public void ToFfmpegArgs_WithMonoChannels_UsesMonoDefaultBitrate()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 0, 1);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-b:a:2", args);
        Assert.Contains("80k", args); // Default for mono
    }

    [Fact]
    public void ToFfmpegArgs_With5_1Channels_Uses5_1DefaultBitrate()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 0, 6);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-b:a:2", args);
        Assert.Contains("384k", args); // Default for 5.1
    }

    [Fact]
    public void ToFfmpegArgs_With7_1Channels_Uses7_1DefaultBitrate()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 0, 8);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-b:a:2", args);
        Assert.Contains("512k", args); // Default for 7.1
    }

    [Fact]
    public void ToFfmpegArgs_WithInvalidChannelCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 0, 3);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => mapping.ToFfmpegArgs());
    }

    [Fact]
    public void ToFfmpegArgs_WithChannels_IncludesChannelArgument()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 192, 2);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-ac:a:2", args);
        Assert.Contains("2", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithZeroChannels_DoesNotIncludeChannelArgument()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 192, 0);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.DoesNotContain("-ac:a:2", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithTitle_IncludesTitleMetadata()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("My Title", 0, 1, 2, "aac", 192, 2);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-metadata:s:a:2", args);
        Assert.Contains("title=\"My Title\"", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithNullCodec_ThrowsArgumentException()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, null!, 192, 2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => mapping.ToFfmpegArgs());
    }

    [Fact]
    public void ToFfmpegArgs_WithEmptyCodec_ThrowsArgumentException()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, string.Empty, 192, 2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => mapping.ToFfmpegArgs());
    }

    [Fact]
    public void ToFfmpegArgs_WithWhitespaceCodec_ThrowsArgumentException()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "   ", 192, 2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => mapping.ToFfmpegArgs());
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping("Title", 0, 1, 2, "aac", 192, 2);

        // Act
        var result = mapping.ToString();

        // Assert
        Assert.Contains("1", result); // SourceIndex
        Assert.Contains("aac", result);
        Assert.Contains("192k", result);
        Assert.Contains("2", result); // DestinationIndex
    }
}
