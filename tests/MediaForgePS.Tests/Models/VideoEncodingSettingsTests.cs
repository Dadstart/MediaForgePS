using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class VideoEncodingSettingsTests
{
    [Theory]
    [InlineData("x264", "yuv420p")]
    [InlineData("libx264", "yuv420p")]
    [InlineData("x265", "yuv420p10le")]
    [InlineData("libx265", "yuv420p10le")]
    [InlineData("vp9", "yuv420p")]
    [InlineData("av1", "yuv420p")]
    [InlineData("h264", "yuv420p")]
    [InlineData("hevc", "yuv420p")]
    public void GetDefaultPixelFormat_WithVariousCodecs_ReturnsExpectedPixelFormat(string codec, string expectedPixelFormat)
    {
        // Act
        var result = VideoEncodingSettings.GetDefaultPixelFormat(codec);

        // Assert
        Assert.Equal(expectedPixelFormat, result);
    }
}
