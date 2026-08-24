using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ExportMediaStreamCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock;
    private readonly Mock<IFfmpegService> _ffmpegServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<ExportMediaStreamCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public ExportMediaStreamCommandTests()
    {
        _pathResolverMock = new Mock<IPathResolver>();
        _ffmpegServiceMock = new Mock<IFfmpegService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<ExportMediaStreamCommand>>();
        _debuggerServiceMock = new Mock<IDebuggerService>();

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));
        _ffmpegServiceMock
            .Setup(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_ffmpegServiceMock.Object);
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
    public void ExportMediaStream_WhenInputPathNotResolved_WritesError()
    {
        var inputPath = @"C:\missing.mkv";
        string? resolvedInputPath = null;
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", @"C:\out.mkv")
            .AddParameter("Type", "Video")
            .AddParameter("Index", 0);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        VerifyConvertNever();
    }

    [Fact]
    public void ExportMediaStream_WhenOutputPathNotResolved_WritesError()
    {
        var inputPath = @"C:\in.mkv";
        var outputPath = @"C:\bad\out.mkv";
        var resolvedInputPath = @"C:\in.mkv";
        string? resolvedOutputPath = null;

        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(p => p.TryResolveOutputPath(outputPath, out resolvedOutputPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("Type", "Video")
            .AddParameter("Index", 0);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        VerifyConvertNever();
    }

    [Theory]
    [InlineData("Video", 0, "0:v:0")]
    [InlineData("Audio", 1, "0:a:1")]
    [InlineData("Subtitle", 2, "0:s:2")]
    [InlineData("Data", 0, "0:d:0")]
    [InlineData("All", 3, "0:3")]
    public void ExportMediaStream_WithValidPaths_InvokesFfmpegService(string type, int index, string mapSpecifier)
    {
        var inputPath = @"C:\in.mkv";
        var outputPath = @"C:\out.mkv";
        SetupResolvedPaths(inputPath, outputPath);

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("Type", type)
            .AddParameter("Index", index);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.Empty(errors);
        VerifyConvertOnce(inputPath, outputPath, ["-map", mapSpecifier, "-c", "copy"]);
    }

    [Fact]
    public void ExportMediaStream_WhenOutputExistsWithoutForce_WritesError()
    {
        var inputPath = @"C:\in.mkv";
        using var outputFile = new TemporaryOutputFile();
        SetupResolvedPaths(inputPath, outputFile.Path, inputPath, outputFile.Path);

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputFile.Path)
            .AddParameter("Type", "Video")
            .AddParameter("Index", 0);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        Assert.Contains("OutputFileExists", errors[0].FullyQualifiedErrorId, StringComparison.Ordinal);
        VerifyConvertNever();
    }

    [Fact]
    public void ExportMediaStream_WhenOutputExistsWithForce_InvokesFfmpegService()
    {
        var inputPath = @"C:\in.mkv";
        using var outputFile = new TemporaryOutputFile();
        SetupResolvedPaths(inputPath, outputFile.Path, inputPath, outputFile.Path);

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputFile.Path)
            .AddParameter("Type", "Audio")
            .AddParameter("Index", 0)
            .AddParameter("Force");

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.Empty(errors);
        VerifyConvertOnce(inputPath, outputFile.Path, ["-map", "0:a:0", "-c", "copy"], overwrite: true);
    }

    [Fact]
    public void ExportMediaStream_WithWhatIf_DoesNotInvokeFfmpegService()
    {
        var inputPath = @"C:\in.mkv";
        var outputPath = @"C:\out.mkv";
        SetupResolvedPaths(inputPath, outputPath);

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("Type", "Video")
            .AddParameter("Index", 0)
            .AddParameter("WhatIf");

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.Empty(errors);
        VerifyConvertNever();
    }

    [Fact]
    public void ExportMediaStream_WhenFfmpegConversionFails_WritesFfmpegExecutionFailed()
    {
        var inputPath = @"C:\in.mkv";
        var outputPath = @"C:\out.mkv";
        SetupResolvedPaths(inputPath, outputPath);
        _ffmpegServiceMock
            .Setup(s => s.ConvertAsync(
                inputPath,
                outputPath,
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new FfmpegConversionException("conversion failed", inputPath, outputPath, 1, "ffmpeg error"));

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("Type", "Video")
            .AddParameter("Index", 0);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        Assert.Contains("FfmpegExecutionFailed", errors[0].FullyQualifiedErrorId, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportMediaStream_WhenFfmpegConversionThrowsInnerException_WritesFfmpegExecutionException()
    {
        var inputPath = @"C:\in.mkv";
        var outputPath = @"C:\out.mkv";
        SetupResolvedPaths(inputPath, outputPath);
        _ffmpegServiceMock
            .Setup(s => s.ConvertAsync(
                inputPath,
                outputPath,
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new FfmpegConversionException(
                "conversion failed",
                inputPath,
                outputPath,
                1,
                "ffmpeg error",
                new InvalidOperationException("inner")));

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("Type", "Video")
            .AddParameter("Index", 0);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        Assert.Contains("FfmpegExecutionException", errors[0].FullyQualifiedErrorId, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportMediaStream_WhenFfmpegServiceThrows_WritesFfmpegExecutionException()
    {
        var inputPath = @"C:\in.mkv";
        var outputPath = @"C:\out.mkv";
        SetupResolvedPaths(inputPath, outputPath);
        _ffmpegServiceMock
            .Setup(s => s.ConvertAsync(
                inputPath,
                outputPath,
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));

        using var ps = CreatePowerShell();
        ps.AddCommand("Export-MediaStream")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("Type", "Video")
            .AddParameter("Index", 0);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
        Assert.Contains("FfmpegExecutionException", errors[0].FullyQualifiedErrorId, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportMediaStream_SupportsShouldProcess()
    {
        var attribute = typeof(ExportMediaStreamCommand)
            .GetCustomAttributes(typeof(CmdletAttribute), inherit: false)
            .Cast<CmdletAttribute>()
            .Single();

        Assert.True(attribute.SupportsShouldProcess);
    }

    private void SetupResolvedPaths(string inputPath, string outputPath)
        => SetupResolvedPaths(inputPath, outputPath, inputPath, outputPath);

    private void SetupResolvedPaths(string inputPath, string outputPath, string resolvedInputPath, string resolvedOutputPath)
    {
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedInputPath))
            .Returns(true);
        _pathResolverMock.Setup(p => p.TryResolveOutputPath(outputPath, out resolvedOutputPath))
            .Returns(true);
    }

    private void VerifyConvertOnce(string inputPath, string outputPath, string[] expectedArguments, bool overwrite = false)
    {
        _ffmpegServiceMock.Verify(
            s => s.ConvertAsync(
                inputPath,
                outputPath,
                It.Is<IEnumerable<string>?>(args => args != null && args.SequenceEqual(expectedArguments)),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(),
                ProcessTimeouts.Extract,
                overwrite),
            Times.Once);
    }

    private void VerifyConvertNever()
    {
        _ffmpegServiceMock.Verify(
            s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<IProgress<FfmpegProgress>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    private static PowerShell CreatePowerShell()
        => PowerShellCmdletTestHost.Create<ExportMediaStreamCommand>("Export-MediaStream");

    private sealed class TemporaryOutputFile : IDisposable
    {
        public TemporaryOutputFile()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MediaForgePS_ExportMediaStream_" + Guid.NewGuid().ToString("N") + ".mkv");
            File.WriteAllText(Path, "existing");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}
