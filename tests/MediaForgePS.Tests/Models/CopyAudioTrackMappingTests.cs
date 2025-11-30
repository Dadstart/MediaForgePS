using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class CopyAudioTrackMappingTests
{
    [Fact]
    public void ToFfmpegArgs_IncludesSourceMap()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping("Title", 0, 1, 2, null);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-map", args);
        Assert.Contains("0:a:1", args);
    }

    [Fact]
    public void ToFfmpegArgs_UsesCopyCodec()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping("Title", 0, 1, 2, null);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-c:a", args);
        Assert.Contains("copy", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithTitle_IncludesTitleMetadata()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping("My Title", 0, 1, 2, null);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-metadata:s:a:2", args);
        Assert.Contains("title=\"My Title\"", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithoutTitle_DoesNotIncludeTitleMetadata()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping(null, 0, 1, 2, null);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.DoesNotContain("-metadata:s:a:2", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithEmptyTitle_DoesNotIncludeTitleMetadata()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping(string.Empty, 0, 1, 2, null);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.DoesNotContain("-metadata:s:a:2", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithWhitespaceTitle_DoesNotIncludeTitleMetadata()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping("   ", 0, 1, 2, null);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.DoesNotContain("-metadata:s:a:2", args);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping("Title", 0, 1, 2, null);

        // Act
        var result = mapping.ToString();

        // Assert
        Assert.Contains("1", result); // SourceIndex
        Assert.Contains("2", result); // DestinationIndex
        Assert.Contains("copy", result);
    }
}
