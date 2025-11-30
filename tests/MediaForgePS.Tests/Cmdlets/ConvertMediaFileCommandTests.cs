using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ConvertMediaFileCommandTests
{
    [Fact]
    public void InputPath_Property_CanBeSet()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand();

        // Act
        cmdlet.InputPath = "input.mkv";

        // Assert
        Assert.Equal("input.mkv", cmdlet.InputPath);
    }

    [Fact]
    public void InputPath_Property_InitializesToEmptyString()
    {
        // Arrange & Act
        var cmdlet = new ConvertMediaFileCommand();

        // Assert
        Assert.Equal(string.Empty, cmdlet.InputPath);
    }

    [Fact]
    public void OutputPath_Property_CanBeSet()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand();

        // Act
        cmdlet.OutputPath = "output.mkv";

        // Assert
        Assert.Equal("output.mkv", cmdlet.OutputPath);
    }

    [Fact]
    public void OutputPath_Property_InitializesToEmptyString()
    {
        // Arrange & Act
        var cmdlet = new ConvertMediaFileCommand();

        // Assert
        Assert.Equal(string.Empty, cmdlet.OutputPath);
    }

    [Fact]
    public void VideoEncodingSettings_Property_CanBeSet()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand();
        var settings = new ConstantRateVideoEncodingSettings("x264", "medium", "high", "film", 23, new List<string>());

        // Act
        cmdlet.VideoEncodingSettings = settings;

        // Assert
        Assert.Same(settings, cmdlet.VideoEncodingSettings);
    }

    [Fact]
    public void AudioTrackMappings_Property_CanBeSet()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand();
        var mappings = new AudioTrackMapping[] { new CopyAudioTrackMapping("Title", 0, 1, 2, null) };

        // Act
        cmdlet.AudioTrackMappings = mappings;

        // Assert
        Assert.Same(mappings, cmdlet.AudioTrackMappings);
    }

    [Fact]
    public void AudioTrackMappings_Property_InitializesToEmptyArray()
    {
        // Arrange & Act
        var cmdlet = new ConvertMediaFileCommand();

        // Assert
        Assert.NotNull(cmdlet.AudioTrackMappings);
        Assert.Empty(cmdlet.AudioTrackMappings);
    }

    [Fact]
    public void AdditionalArguments_Property_CanBeSet()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand();
        var args = new[] { "-extra", "value" };

        // Act
        cmdlet.AdditionalArguments = args;

        // Assert
        Assert.Same(args, cmdlet.AdditionalArguments);
    }

    [Fact]
    public void AdditionalArguments_Property_InitializesToNull()
    {
        // Arrange & Act
        var cmdlet = new ConvertMediaFileCommand();

        // Assert
        Assert.Null(cmdlet.AdditionalArguments);
    }
}
