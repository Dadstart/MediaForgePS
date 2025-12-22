using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ConvertMediaFileCommandTests : CmdletTestBase
{
    [Fact]
    public void Process_WhenInputPathNotFound_DoesNotCallFfmpegService()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand
        {
            InputPath = "nonexistent.mp4",
            OutputPath = "output.mp4",
            VideoEncodingSettings = new ConstantRateVideoEncodingSettings(
                "libx264", "medium", "high", "film", 23),
            AudioTrackMappings = Array.Empty<AudioTrackMapping>()
        };

        InjectMockedServices(cmdlet);

        string? outValue = null;
        MockPathResolver
            .Setup(r => r.TryResolveInputPath(It.IsAny<string>(), out outValue))
            .Returns(false);

        // Act - Use reflection to call protected Process method
        var processMethod = typeof(ConvertMediaFileCommand).GetMethod("Process",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        processMethod?.Invoke(cmdlet, null);

        // Assert
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Process_WhenOutputPathResolutionFails_DoesNotCallFfmpegService()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand
        {
            InputPath = "input.mp4",
            OutputPath = "output.mp4",
            VideoEncodingSettings = new ConstantRateVideoEncodingSettings(
                "libx264", "medium", "high", "film", 23),
            AudioTrackMappings = Array.Empty<AudioTrackMapping>()
        };

        InjectMockedServices(cmdlet);

        string resolvedInputPath = "resolved_input.mp4";
        MockPathResolver
            .Setup(r => r.TryResolveInputPath(It.IsAny<string>(), out resolvedInputPath))
            .Returns(true);

        string? outValue = null;
        MockPathResolver
            .Setup(r => r.TryResolveOutputPath(It.IsAny<string>(), out outValue))
            .Returns(false);

        // Act - Use reflection to call protected Process method
        var processMethod = typeof(ConvertMediaFileCommand).GetMethod("Process",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        processMethod?.Invoke(cmdlet, null);

        // Assert
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Process_WhenConversionSucceeds_CallsFfmpegServiceWithCorrectParameters()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand
        {
            InputPath = "input.mp4",
            OutputPath = "output.mp4",
            VideoEncodingSettings = new ConstantRateVideoEncodingSettings(
                "libx264", "medium", "high", "film", 23),
            AudioTrackMappings = Array.Empty<AudioTrackMapping>()
        };

        InjectMockedServices(cmdlet);

        string resolvedInputPath = "resolved_input.mp4";
        string resolvedOutputPath = "resolved_output.mp4";
        MockPathResolver
            .Setup(r => r.TryResolveInputPath(It.IsAny<string>(), out resolvedInputPath))
            .Returns(true);

        MockPathResolver
            .Setup(r => r.TryResolveOutputPath(It.IsAny<string>(), out resolvedOutputPath))
            .Returns(true);

        MockFfmpegService
            .Setup(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Use reflection to call protected Process method
        var processMethod = typeof(ConvertMediaFileCommand).GetMethod("Process",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        processMethod?.Invoke(cmdlet, null);

        // Assert
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                It.IsAny<IEnumerable<string>>(),
                System.Threading.CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public void Process_WhenConversionFails_StillCallsFfmpegService()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand
        {
            InputPath = "input.mp4",
            OutputPath = "output.mp4",
            VideoEncodingSettings = new ConstantRateVideoEncodingSettings(
                "libx264", "medium", "high", "film", 23),
            AudioTrackMappings = Array.Empty<AudioTrackMapping>()
        };

        InjectMockedServices(cmdlet);

        string resolvedInputPath = "resolved_input.mp4";
        string resolvedOutputPath = "resolved_output.mp4";
        MockPathResolver
            .Setup(r => r.TryResolveInputPath(It.IsAny<string>(), out resolvedInputPath))
            .Returns(true);

        MockPathResolver
            .Setup(r => r.TryResolveOutputPath(It.IsAny<string>(), out resolvedOutputPath))
            .Returns(true);

        MockFfmpegService
            .Setup(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(false);

        // Act - Use reflection to call protected Process method
        var processMethod = typeof(ConvertMediaFileCommand).GetMethod("Process",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        processMethod?.Invoke(cmdlet, null);

        // Assert
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                It.IsAny<IEnumerable<string>>(),
                System.Threading.CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public void Process_WhenExceptionOccurs_HandlesException()
    {
        // Arrange
        var cmdlet = new ConvertMediaFileCommand
        {
            InputPath = "input.mp4",
            OutputPath = "output.mp4",
            VideoEncodingSettings = new ConstantRateVideoEncodingSettings(
                "libx264", "medium", "high", "film", 23),
            AudioTrackMappings = Array.Empty<AudioTrackMapping>()
        };

        InjectMockedServices(cmdlet);

        string resolvedInputPath = "resolved_input.mp4";
        string resolvedOutputPath = "resolved_output.mp4";
        MockPathResolver
            .Setup(r => r.TryResolveInputPath(It.IsAny<string>(), out resolvedInputPath))
            .Returns(true);

        MockPathResolver
            .Setup(r => r.TryResolveOutputPath(It.IsAny<string>(), out resolvedOutputPath))
            .Returns(true);

        MockFfmpegService
            .Setup(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert - Should not throw
        // Use reflection to call protected Process method
        var processMethod = typeof(ConvertMediaFileCommand).GetMethod("Process",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        processMethod?.Invoke(cmdlet, null);

        // Verify the service was called
        MockFfmpegService.Verify(
            s => s.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                It.IsAny<IEnumerable<string>>(),
                System.Threading.CancellationToken.None),
            Times.Once);
    }
}
