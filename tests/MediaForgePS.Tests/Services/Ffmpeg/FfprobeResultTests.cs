using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfprobeResultTests
{
    [Fact]
    public void Constructor_WithSuccess_SetsProperties()
    {
        // Arrange
        var success = true;
        var json = "{\"format\": {}}";

        // Act
        var result = new FfprobeResult(success, json);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(json, result.Json);
    }

    [Fact]
    public void Constructor_WithFailure_SetsProperties()
    {
        // Arrange
        var success = false;
        var json = string.Empty;

        // Act
        var result = new FfprobeResult(success, json);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(json, result.Json);
    }
}
