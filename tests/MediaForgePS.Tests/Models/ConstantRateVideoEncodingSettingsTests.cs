using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class ConstantRateVideoEncodingSettingsTests
{
    [Fact]
    public void IsSinglePass_Always_ReturnsTrue()
    {
        // Arrange
        var settings = new ConstantRateVideoEncodingSettings("x264", "medium", "high", "film", 23, new List<string>());

        // Act & Assert
        Assert.True(settings.IsSinglePass);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var settings = new ConstantRateVideoEncodingSettings("x264", "medium", "high", "film", 23, new List<string>());

        // Act
        var result = settings.ToString();

        // Assert
        Assert.Contains("x264", result);
        Assert.Contains("CRF", result);
        Assert.Contains("23", result);
        Assert.Contains("medium", result);
    }

    [Fact]
    public void ToFfmpegArgs_WithX264Codec_ConvertsToLibx264()
    {
        // Arrange
        var settings = new ConstantRateVideoEncodingSettings("x264", "medium", "high", "film", 23, new List<string>());

        // Act
        var args = settings.ToFfmpegArgs(null);

        // Assert
        Assert.Contains("-c:v", args);
        Assert.Contains("libx264", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithNonX264Codec_UsesCodecAsIs()
    {
        // Arrange
        var settings = new ConstantRateVideoEncodingSettings("libx265", "medium", "high", "film", 23, new List<string>());

        // Act
        var args = settings.ToFfmpegArgs(null);

        // Assert
        Assert.Contains("-c:v", args);
        Assert.Contains("libx265", args);
    }

    [Fact]
    public void ToFfmpegArgs_IncludesRequiredArguments()
    {
        // Arrange
        var settings = new ConstantRateVideoEncodingSettings("x264", "medium", "high", "film", 23, new List<string>());

        // Act
        var args = settings.ToFfmpegArgs(null);

        // Assert
        Assert.Contains("-map", args);
        Assert.Contains("0:v:0", args);
        Assert.Contains("-preset", args);
        Assert.Contains("medium", args);
        Assert.Contains("-crf", args);
        Assert.Contains("23", args);
        Assert.Contains("-pix_fmt", args);
        Assert.Contains("yuv420p", args);
        Assert.Contains("-map_metadata", args);
        Assert.Contains("0", args);
        Assert.Contains("-map_chapters", args);
        Assert.Contains("-movflags", args);
        Assert.Contains("+faststart", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithAdditionalArgs_IncludesAdditionalArgs()
    {
        // Arrange
        var additionalArgs = new List<string> { "-extra", "value" };
        var settings = new ConstantRateVideoEncodingSettings("x264", "medium", "high", "film", 23, additionalArgs);

        // Act
        var args = settings.ToFfmpegArgs(null);

        // Assert
        Assert.Contains("-extra", args);
        Assert.Contains("value", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithNullPass_DoesNotThrow()
    {
        // Arrange
        var settings = new ConstantRateVideoEncodingSettings("x264", "medium", "high", "film", 23, new List<string>());

        // Act & Assert
        var args = settings.ToFfmpegArgs(null);
        Assert.NotNull(args);
    }

    [Fact]
    public void ToFfmpegArgs_WithPassValue_IgnoresPass()
    {
        // Arrange
        var settings = new ConstantRateVideoEncodingSettings("x264", "medium", "high", "film", 23, new List<string>());

        // Act
        var args1 = settings.ToFfmpegArgs(1);
        var args2 = settings.ToFfmpegArgs(2);

        // Assert
        // Should produce same arguments regardless of pass
        Assert.Equal(args1, args2);
    }
}
