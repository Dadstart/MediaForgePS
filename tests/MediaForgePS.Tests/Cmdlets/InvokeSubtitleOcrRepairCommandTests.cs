using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class InvokeSubtitleOcrRepairCommandTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly Mock<IImageSubtitleOcrConverter> _ocrConverterMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public InvokeSubtitleOcrRepairCommandTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger<InvokeSubtitleOcrRepairCommand>>();
        var pathResolverLoggerMock = new Mock<ILogger<PathResolver>>();
        _debuggerServiceMock = new Mock<IDebuggerService>();
        _ocrConverterMock = new Mock<IImageSubtitleOcrConverter>();

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns((string name) =>
            {
                if (name?.Contains("PathResolver") == true)
                    return pathResolverLoggerMock.Object;
                return loggerMock.Object;
            });
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));
        _ocrConverterMock.SetupGet(c => c.IsSupportedOnCurrentPlatform).Returns(true);
        _ocrConverterMock.SetupGet(c => c.IsAvailable).Returns(true);
        _ocrConverterMock.SetupGet(c => c.ExpectedTessDataDescription).Returns("tessdata expected");
        _ocrConverterMock
            .Setup(c => c.ConvertToSrt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, outputPath, _) =>
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(outputPath, "1\n00:00:00,000 --> 00:00:01,000\n\n");
            });

        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton<ILogger<PathResolver>>(pathResolverLoggerMock.Object);
        services.AddSingleton<IPathResolver, PathResolver>();
        services.AddSingleton<IImageSubtitleOcrConverter>(_ocrConverterMock.Object);
        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void InvokeSubtitleOcrRepair_WhenPathDoesNotExist_WritesError()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-SubtitleOcrRepair").AddParameter("InputPath", new[] { "C:\\Nonexistent\\path.sup" });

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void InvokeSubtitleOcrRepair_WhenDirectoryHasNoSubtitleFiles_WritesWarning()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_InvokeSubtitleOcrRepair_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(emptyDir);

            using var ps = CreatePowerShell();
            ps.AddCommand("Invoke-SubtitleOcrRepair").AddParameter("InputPath", new[] { emptyDir });

            ps.Invoke();
            var warnings = ps.Streams.Warning.ReadAll();

            Assert.NotEmpty(warnings);
        }
        finally
        {
            if (Directory.Exists(emptyDir))
                Directory.Delete(emptyDir);
        }
    }

    [Fact]
    public void InvokeSubtitleOcrRepair_WhenNoInputPathsProvided_WritesWarning()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-SubtitleOcrRepair").AddParameter("InputPath", new[] { "   ", "\t" });

        ps.Invoke();
        var warnings = ps.Streams.Warning.ReadAll();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(warnings, warning => warning.Message.Contains("No input path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvokeSubtitleOcrRepair_WhenWhatIfSpecified_ReturnsEmptySubtitleProcessingResult()
    {
        var tempDir = CreateTempDirectory();
        var supPath = Path.Combine(tempDir, "clip.sup");
        File.WriteAllText(supPath, "sup");

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-SubtitleOcrRepair")
            .AddParameter("InputPath", supPath)
            .AddParameter("WhatIf")
            .AddParameter("Confirm", false);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results).BaseObject);
        Assert.Equal(0, result.ConvertedCount);
        _ocrConverterMock.Verify(
            converter => converter.ConvertToSrt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void InvokeSubtitleOcrRepair_WhenExistingSrtOnlyAndSkipRepair_ReturnsZeroConverted()
    {
        var tempDir = CreateTempDirectory();
        var srtPath = Path.Combine(tempDir, "clip.srt");
        File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\nHello\n");

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-SubtitleOcrRepair")
            .AddParameter("InputPath", srtPath)
            .AddParameter("SkipRepair")
            .AddParameter("Confirm", false);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results).BaseObject);
        Assert.Equal(0, result.ConvertedCount);
        Assert.Equal("Hello", File.ReadAllText(srtPath).Split('\n')[2]);
    }

    [Fact]
    public void InvokeSubtitleOcrRepair_WhenSingleSupFileSucceeds_OutputsSubtitleProcessingResult()
    {
        var tempDir = CreateTempDirectory();
        var supPath = Path.Combine(tempDir, "clip.sup");
        File.WriteAllText(supPath, "sup");

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-SubtitleOcrRepair")
            .AddParameter("InputPath", supPath)
            .AddParameter("SkipRepair")
            .AddParameter("Confirm", false);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results).BaseObject);
        Assert.Equal(1, result.ConvertedCount);
        Assert.True(File.Exists(Path.ChangeExtension(supPath, ".srt")));
    }

    [Fact]
    public void InvokeSubtitleOcrRepair_WhenPlatformUnsupported_WritesWarningAndError()
    {
        _ocrConverterMock.SetupGet(c => c.IsSupportedOnCurrentPlatform).Returns(false);

        var tempDir = CreateTempDirectory();
        var supPath = Path.Combine(tempDir, "clip.sup");
        File.WriteAllText(supPath, "sup");

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-SubtitleOcrRepair")
            .AddParameter("InputPath", supPath)
            .AddParameter("Confirm", false);

        ps.Invoke();
        var warnings = ps.Streams.Warning.ReadAll();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(warnings);
        Assert.NotEmpty(errors);
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_InvokeSubtitleOcrRepair_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static PowerShell CreatePowerShell() =>
        PowerShellCmdletTestHost.Create<InvokeSubtitleOcrRepairCommand>("Invoke-SubtitleOcrRepair");
}
