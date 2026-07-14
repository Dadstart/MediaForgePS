using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ConvertMediaFileAdvancedCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock;
    private readonly Mock<IMediaConversionService> _mediaConversionServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<ConvertMediaFileAdvancedCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public ConvertMediaFileAdvancedCommandTests()
    {
        _pathResolverMock = new Mock<IPathResolver>();
        _mediaConversionServiceMock = new Mock<IMediaConversionService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<ConvertMediaFileAdvancedCommand>>();
        _debuggerServiceMock = new Mock<IDebuggerService>();

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_mediaConversionServiceMock.Object);
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void ConvertMediaFileAdvanced_WhenInputPathNotResolved_WritesError()
    {
        var inputPath = "C:\\missing.mkv";
        string? resolvedInputPath = null;
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFileAdvanced")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", "C:\\out.mp4")
            .AddParameter("VideoEncodingSettings", CreateVideoSettings())
            .AddParameter("AudioTrackMappings", CreateAudioTrackMappings());

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        _mediaConversionServiceMock.Verify(
            s => s.ExecuteConversion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VideoEncodingSettings>(), It.IsAny<AudioTrackMapping[]>(), It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ConvertMediaFileAdvanced_WhenOutputPathNotResolved_WritesError()
    {
        var inputPath = "C:\\in.mkv";
        var outputPath = "C:\\bad\\out.mp4";
        var resolvedInputPath = "C:\\in.mkv";
        string? resolvedOutputPath = null;

        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(p => p.TryResolveOutputPath(outputPath, out resolvedOutputPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFileAdvanced")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("VideoEncodingSettings", CreateVideoSettings())
            .AddParameter("AudioTrackMappings", CreateAudioTrackMappings());

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        _mediaConversionServiceMock.Verify(
            s => s.ExecuteConversion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VideoEncodingSettings>(), It.IsAny<AudioTrackMapping[]>(), It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ConvertMediaFileAdvanced_WithValidPaths_InvokesConversionService()
    {
        var inputPath = "C:\\in.mkv";
        var outputPath = "C:\\out.mp4";
        var resolvedInputPath = "C:\\in.mkv";
        var resolvedOutputPath = "C:\\out.mp4";

        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(p => p.TryResolveOutputPath(outputPath, out resolvedOutputPath))
            .Returns(true);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFileAdvanced")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("VideoEncodingSettings", CreateVideoSettings(codec: "libx265"))
            .AddParameter("AudioTrackMappings", CreateAudioTrackMappings())
            .AddParameter("X265Params", "aq-mode=3");

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.Single(results);
        var conversionResult = Assert.IsType<MediaConversionResult>(result.BaseObject);
        Assert.True(conversionResult.Success);
        Assert.Equal(resolvedOutputPath, conversionResult.OutputPath);
        Assert.Equal("Success", conversionResult.Status);
        Assert.True(conversionResult.ProcessingTime >= TimeSpan.Zero);
        _mediaConversionServiceMock.Verify(
            s => s.ExecuteConversion(
                resolvedInputPath,
                resolvedOutputPath,
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.Is<string[]?>(args => args != null && args.SequenceEqual(new[] { "-x265-params", "aq-mode=3" })),
                It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ConvertMediaFileAdvanced_WhenWindowTitleSupported_SetsAndRestoresTerminalTitle()
    {
        var inputPath = "C:\\in.mkv";
        var outputPath = "C:\\out.mp4";
        var resolvedInputPath = "C:\\in.mkv";
        var resolvedOutputPath = "C:\\out.mp4";

        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(p => p.TryResolveOutputPath(outputPath, out resolvedOutputPath))
            .Returns(true);

        string? titleDuringConversion = null;
        _mediaConversionServiceMock.Setup(
            s => s.ExecuteConversion(
                resolvedInputPath,
                resolvedOutputPath,
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var currentCmdlet = CmdletContext.Current as PSCmdlet;
                if (currentCmdlet == null)
                    return;

                try
                {
                    titleDuringConversion = currentCmdlet.Host.UI.RawUI.WindowTitle;
                }
                catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
                {
                    titleDuringConversion = null;
                }
            });

        using var ps = CreatePowerShell();
        var originalTitle = TryReadWindowTitle(ps);
        ps.Commands.Clear();

        ps.AddCommand("Convert-MediaFileAdvanced")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("VideoEncodingSettings", CreateVideoSettings(codec: "libx265"))
            .AddParameter("AudioTrackMappings", CreateAudioTrackMappings())
            .AddParameter("X265Params", "aq-mode=3");

        _ = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);

        var finalTitle = TryReadWindowTitle(ps);
        if (originalTitle == null || finalTitle == null || titleDuringConversion == null)
            return;

        Assert.Contains("Convert-MediaFileAdvanced", titleDuringConversion, StringComparison.Ordinal);
        Assert.Equal(originalTitle, finalTitle);
    }

    [Fact]
    public void ConvertMediaFileAdvanced_WithWhatIf_DoesNotInvokeConversionService()
    {
        var inputPath = "C:\\in.mkv";
        var outputPath = "C:\\out.mp4";
        var resolvedInputPath = "C:\\in.mkv";
        var resolvedOutputPath = "C:\\out.mp4";

        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(p => p.TryResolveOutputPath(outputPath, out resolvedOutputPath))
            .Returns(true);

        using var ps = CreatePowerShell();
        ps.AddCommand("Convert-MediaFileAdvanced")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("VideoEncodingSettings", CreateVideoSettings())
            .AddParameter("AudioTrackMappings", CreateAudioTrackMappings())
            .AddParameter("WhatIf");

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(
            s => s.ExecuteConversion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VideoEncodingSettings>(), It.IsAny<AudioTrackMapping[]>(), It.IsAny<string[]?>(), It.IsAny<IProgress<FfmpegProgress>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ConvertMediaFileAdvanced_SupportsShouldProcess()
    {
        var attribute = typeof(ConvertMediaFileAdvancedCommand)
            .GetCustomAttributes(typeof(CmdletAttribute), inherit: false)
            .Cast<CmdletAttribute>()
            .Single();

        Assert.True(attribute.SupportsShouldProcess);
    }

    private static ConstantRateVideoEncodingSettings CreateVideoSettings(string codec = "libx264")
    {
        return new ConstantRateVideoEncodingSettings(
            codec,
            "medium",
            "high",
            "film",
            20,
            VideoEncodingSettings.GetDefaultPixelFormat(codec));
    }

    private static AudioTrackMapping[] CreateAudioTrackMappings()
    {
        return [new CopyAudioTrackMapping("eng", 0, 0, 0)];
    }

    private static PowerShell CreatePowerShell()
    {
        return PowerShellCmdletTestHost.Create<ConvertMediaFileAdvancedCommand>("Convert-MediaFileAdvanced");
    }

    private static string? TryReadWindowTitle(PowerShell powerShell)
    {
        powerShell.Commands.Clear();
        powerShell.Streams.Error.Clear();
        powerShell.AddScript("$Host.UI.RawUI.WindowTitle");
        var results = powerShell.Invoke<string>().ToList();
        var errors = powerShell.Streams.Error.ReadAll();
        if (errors.Count > 0)
            return null;

        return results.FirstOrDefault();
    }
}
