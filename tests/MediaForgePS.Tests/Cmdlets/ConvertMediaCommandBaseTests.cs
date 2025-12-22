using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ConvertMediaCommandBaseTests : CmdletTestBase
{
    [Fact]
    public void BuildFfmpegArguments_WithSinglePassSettings_ReturnsCorrectArguments()
    {
        // Arrange
        var videoSettings = new ConstantRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 23);
        var audioMappings = new AudioTrackMapping[]
        {
            new CopyAudioTrackMapping("English", 1, 0, 0)
        };
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings);

        // Act
        var args = cmdlet.BuildFfmpegArguments(null).ToList();

        // Assert
        Assert.NotEmpty(args);
        // Verify that video encoding arguments are included
        Assert.Contains(args, a => a.Contains("libx264") || a.Contains("crf") || a.Contains("preset"));
    }

    [Fact]
    public void BuildFfmpegArguments_WithAdditionalArguments_IncludesAdditionalArguments()
    {
        // Arrange
        var videoSettings = new ConstantRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 23);
        var audioMappings = Array.Empty<AudioTrackMapping>();
        var additionalArgs = new[] { "-extra1", "-extra2" };
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings, additionalArgs);

        // Act
        var args = cmdlet.BuildFfmpegArguments(null).ToList();

        // Assert
        Assert.Contains("-extra1", args);
        Assert.Contains("-extra2", args);
    }

    [Fact]
    public void BuildFfmpegArguments_WithNullAdditionalArguments_DoesNotThrow()
    {
        // Arrange
        var videoSettings = new ConstantRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 23);
        var audioMappings = Array.Empty<AudioTrackMapping>();
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings, null);

        // Act & Assert
        var args = cmdlet.BuildFfmpegArguments(null).ToList();
        Assert.NotEmpty(args);
    }

    [Fact]
    public void BuildFfmpegArguments_WithTwoPassSettings_ReturnsDifferentArgumentsForEachPass()
    {
        // Arrange
        var videoSettings = new VariableRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 5000);
        var audioMappings = Array.Empty<AudioTrackMapping>();
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings);

        // Act
        var pass1Args = cmdlet.BuildFfmpegArguments(1).ToList();
        var pass2Args = cmdlet.BuildFfmpegArguments(2).ToList();

        // Assert
        Assert.NotEmpty(pass1Args);
        Assert.NotEmpty(pass2Args);
        // Pass 1 should have different arguments than pass 2
        Assert.NotEqual(pass1Args, pass2Args);
    }

    [Fact]
    public void CreatePathErrorRecord_CreatesErrorRecordWithCorrectProperties()
    {
        // Arrange
        var videoSettings = new ConstantRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 23);
        var audioMappings = Array.Empty<AudioTrackMapping>();
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings);
        var exception = new Exception("Test error");
        const string errorId = "TestError";
        const string targetPath = "test/path";

        // Act
        var errorRecord = cmdlet.CreatePathErrorRecord(
            exception, errorId, System.Management.Automation.ErrorCategory.InvalidArgument, targetPath);

        // Assert
        Assert.NotNull(errorRecord);
        Assert.Equal(exception, errorRecord.Exception);
        Assert.Equal(errorId, errorRecord.FullyQualifiedErrorId);
        Assert.Equal(targetPath, errorRecord.TargetObject);
        Assert.Equal(System.Management.Automation.ErrorCategory.InvalidArgument, errorRecord.CategoryInfo.Category);
    }

    [Fact]
    public void ConvertMediaFile_WithSinglePassSettings_CallsFfmpegServiceOnce()
    {
        // Arrange
        var videoSettings = new ConstantRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 23);
        var audioMappings = Array.Empty<AudioTrackMapping>();
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings);

        MockFfmpegService
            .Setup(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        const string inputPath = "input.mp4";
        const string outputPath = "output.mp4";

        // Act
        var result = cmdlet.ConvertMediaFile(inputPath, outputPath);

        // Assert
        Assert.True(result);
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                inputPath,
                outputPath,
                It.IsAny<IEnumerable<string>>(),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public void ConvertMediaFile_WithTwoPassSettings_CallsFfmpegServiceTwice()
    {
        // Arrange
        var videoSettings = new VariableRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 5000);
        var audioMappings = Array.Empty<AudioTrackMapping>();
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings);

        MockFfmpegService
            .Setup(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        const string inputPath = "input.mp4";
        const string outputPath = "output.mp4";

        // Act
        var result = cmdlet.ConvertMediaFile(inputPath, outputPath);

        // Assert
        Assert.True(result);
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                inputPath,
                outputPath,
                It.IsAny<IEnumerable<string>>(),
                CancellationToken.None),
            Times.Exactly(2));
    }

    [Fact]
    public void ConvertMediaFile_WhenFirstPassFails_ReturnsFalse()
    {
        // Arrange
        var videoSettings = new VariableRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 5000);
        var audioMappings = Array.Empty<AudioTrackMapping>();
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings);

        MockFfmpegService
            .SetupSequence(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        const string inputPath = "input.mp4";
        const string outputPath = "output.mp4";

        // Act
        var result = cmdlet.ConvertMediaFile(inputPath, outputPath);

        // Assert
        Assert.False(result);
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                inputPath,
                outputPath,
                It.IsAny<IEnumerable<string>>(),
                CancellationToken.None),
            Times.Once); // Should only call once since first pass fails
    }

    [Fact]
    public void ConvertMediaFile_WhenSecondPassFails_ReturnsFalse()
    {
        // Arrange
        var videoSettings = new VariableRateVideoEncodingSettings(
            "libx264", "medium", "high", "film", 5000);
        var audioMappings = Array.Empty<AudioTrackMapping>();
        var cmdlet = CreateTestableConvertMediaCommandBase(videoSettings, audioMappings);

        MockFfmpegService
            .SetupSequence(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        const string inputPath = "input.mp4";
        const string outputPath = "output.mp4";

        // Act
        var result = cmdlet.ConvertMediaFile(inputPath, outputPath);

        // Assert
        Assert.False(result);
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                inputPath,
                outputPath,
                It.IsAny<IEnumerable<string>>(),
                CancellationToken.None),
            Times.Exactly(2)); // Should call twice (both passes)
    }
}
