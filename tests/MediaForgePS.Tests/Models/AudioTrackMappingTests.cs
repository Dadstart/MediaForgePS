using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class AudioTrackMappingTests
{
    [Fact]
    public void AddTitleMetadata_WithTitle_AddsMetadataArgs()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping("My Title", 0, 1, 2, null);
        var args = new List<string>();

        // Act
        // Access protected method via reflection or test through public method
        var ffmpegArgs = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-metadata:s:a:2", ffmpegArgs);
        Assert.Contains("title=\"My Title\"", ffmpegArgs);
    }

    [Fact]
    public void AddTitleMetadata_WithoutTitle_DoesNotAddMetadataArgs()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping(null, 0, 1, 2, null);
        var args = new List<string>();

        // Act
        var ffmpegArgs = mapping.ToFfmpegArgs();

        // Assert
        Assert.DoesNotContain("-metadata:s:a:2", ffmpegArgs);
    }

    [Fact]
    public void AddSourceMapArgs_AddsMapArgs()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping(null, 0, 1, 2, null);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-map", args);
        Assert.Contains("0:a:1", args);
    }

    [Fact]
    public void AddDestinationCodecArgs_WithCodec_AddsCodecArgs()
    {
        // Arrange
        var mapping = new CopyAudioTrackMapping(null, 0, 1, 2, null);

        // Act
        var args = mapping.ToFfmpegArgs();

        // Assert
        Assert.Contains("-c:a", args);
        Assert.Contains("copy", args);
    }

    [Fact]
    public void AddDestinationCodecArgs_WithNullCodec_ThrowsArgumentException()
    {
        // Arrange
        var mapping = new EncodeAudioTrackMapping(null, 0, 1, 2, null!, 0, 0);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => mapping.ToFfmpegArgs());
    }
}
