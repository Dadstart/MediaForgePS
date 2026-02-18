using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

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

        var moduleServicesType = typeof(ModuleServices);
        _providerField = moduleServicesType.GetField("_provider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        _initializedField = moduleServicesType.GetField("_initialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        InjectServiceProvider();
    }

    public void Dispose()
    {
        if (_providerField != null)
            _providerField.SetValue(null, null);
        if (_initializedField != null)
            _initializedField.SetValue(null, false);
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
            s => s.ExecuteConversion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VideoEncodingSettings>(), It.IsAny<AudioTrackMapping[]>(), It.IsAny<string[]?>()),
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
            s => s.ExecuteConversion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VideoEncodingSettings>(), It.IsAny<AudioTrackMapping[]>(), It.IsAny<string[]?>()),
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

        Assert.Empty(results);
        Assert.Empty(errors);
        _mediaConversionServiceMock.Verify(
            s => s.ExecuteConversion(
                resolvedInputPath,
                resolvedOutputPath,
                It.IsAny<VideoEncodingSettings>(),
                It.IsAny<AudioTrackMapping[]>(),
                It.Is<string[]?>(args => args != null && args.SequenceEqual(new[] { "-x265-params", "aq-mode=3" }))),
            Times.Once);
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

    private void InjectServiceProvider()
    {
        if (_providerField != null)
            _providerField.SetValue(null, _serviceProvider);
        if (_initializedField != null)
            _initializedField.SetValue(null, true);
    }

    private static PowerShell CreatePowerShell()
    {
        var asm = typeof(ConvertMediaFileAdvancedCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Convert-MediaFileAdvanced", typeof(ConvertMediaFileAdvancedCommand), null));
        return PowerShell.Create(initialSessionState);
    }
}
