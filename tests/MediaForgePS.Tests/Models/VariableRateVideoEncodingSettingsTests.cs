using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class VariableRateVideoEncodingSettingsTests
{
    [Fact]
    public void IsSinglePass_Always_ReturnsFalse()
    {
        // Arrange
        var settings = new VariableRateVideoEncodingSettings("x264", "medium", "high", "film", 5000, new List<string>());

        // Act & Assert
        Assert.False(settings.IsSinglePass);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var settings = new VariableRateVideoEncodingSettings("x264", "medium", "high", "film", 5000, new List<string>());

        // Act
        var result = settings.ToString();

        // Assert
        Assert.Contains("x264", result);
        Assert.Contains("5000k", result);
        Assert.Contains("medium", result);
    }

    [Fact]
    public void ToFfmpegArgs_WithPass1_DoesNotIncludeMapArguments()
    {
        // Arrange
        var settings = new VariableRateVideoEncodingSettings("x264", "medium", "high", "film", 5000, new List<string>());

        // Act
        var args = settings.ToFfmpegArgs(1);

        // Assert
        Assert.DoesNotContain("-map", args);
        Assert.DoesNotContain("0:v:0", args);
        Assert.DoesNotContain("-map_metadata", args);
        Assert.DoesNotContain("-map_chapters", args);
        Assert.DoesNotContain("-movflags", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithPass2_IncludesMapArguments()
    {
        // Arrange
        var settings = new VariableRateVideoEncodingSettings("x264", "medium", "high", "film", 5000, new List<string>());

        // Act
        var args = settings.ToFfmpegArgs(2);

        // Assert
        Assert.Contains("-map", args);
        Assert.Contains("0:v:0", args);
        Assert.Contains("-map_metadata", args);
        Assert.Contains("0", args);
        Assert.Contains("-map_chapters", args);
        Assert.Contains("-movflags", args);
        Assert.Contains("+faststart", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithX264Codec_ConvertsToLibx264()
    {
        // Arrange
        var settings = new VariableRateVideoEncodingSettings("x264", "medium", "high", "film", 5000, new List<string>());

        // Act
        var args = settings.ToFfmpegArgs(1);

        // Assert
        Assert.Contains("-c:v", args);
        Assert.Contains("libx264", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithNonX264Codec_UsesCodecAsIs()
    {
        // Arrange
        var settings = new VariableRateVideoEncodingSettings("libx265", "medium", "high", "film", 5000, new List<string>());

        // Act
        var args = settings.ToFfmpegArgs(1);

        // Assert
        Assert.Contains("-c:v", args);
        Assert.Contains("libx265", args);
    }

    [Fact]
    public void ToFfmpegArgs_IncludesBitrate()
    {
        // Arrange
        var settings = new VariableRateVideoEncodingSettings("x264", "medium", "high", "film", 5000, new List<string>());

        // Act
        var args = settings.ToFfmpegArgs(1);

        // Assert
        Assert.Contains("-b:v", args);
        Assert.Contains("5000k", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithAdditionalArgs_IncludesAdditionalArgs()
    {
        // Arrange
        var additionalArgs = new List<string> { "-extra", "value" };
        var settings = new VariableRateVideoEncodingSettings("x264", "medium", "high", "film", 5000, additionalArgs);

        // Act
        var args = settings.ToFfmpegArgs(1);

        // Assert
        Assert.Contains("-extra", args);
        Assert.Contains("value", args);
    }

    [Fact]
    public void ToFfmpegArgs_WithInvalidPass_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var settings = new VariableRateVideoEncodingSettings("x264", "medium", "high", "film", 5000, new List<string>());

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToFfmpegArgs(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToFfmpegArgs(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToFfmpegArgs(3));
    }
}
