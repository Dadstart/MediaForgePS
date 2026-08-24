using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class InvokeSeriesProcessingCommandTests : IDisposable
{
    private readonly Mock<ISeriesProcessingService> _seriesProcessingServiceMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<IDebuggerService> _debuggerServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;
    private readonly List<string> _tempDirectories = [];

    public InvokeSeriesProcessingCommandTests()
    {
        _loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
        _debuggerServiceMock.Setup(debugger => debugger.BreakIfDebugging(It.IsAny<bool>()));

        var ocrConverterMock = new Mock<IImageSubtitleOcrConverter>();
        ocrConverterMock.SetupGet(converter => converter.IsSupportedOnCurrentPlatform).Returns(false);

        var services = new ServiceCollection();
        services.AddSingleton(_seriesProcessingServiceMock.Object);
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton(Mock.Of<IPathResolver>());
        services.AddSingleton(ocrConverterMock.Object);
        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();

        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void InvokeSeriesProcessing_WhenSeasonScanReturnsNoEpisodes_WritesError()
    {
        SetupDirectoryStructure();
        _seriesProcessingServiceMock
            .Setup(service => service.InvokeSeasonScan(It.IsAny<ICmdletIO>(), 1, null, null, It.IsAny<CancellationToken>()))
            .Returns(Array.Empty<TvDbEpisodeInfo>());

        using var ps = CreatePowerShell();
        AddCommonParameters(ps, CreateTempDirectory());

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Contains(errors, error =>
            error.Exception is InvalidOperationException exception
            && exception.Message.Contains("Season scanning failed", StringComparison.Ordinal));
        _seriesProcessingServiceMock.Verify(
            service => service.InvokeVideoCopy(It.IsAny<ICmdletIO>(), It.IsAny<VideoCopyRequest>()),
            Times.Never);
    }

    [Fact]
    public void InvokeSeriesProcessing_WhenNoFilesCopied_WritesError()
    {
        SetupDirectoryStructure();
        SetupSeasonScan();
        _seriesProcessingServiceMock
            .Setup(service => service.InvokeVideoCopy(It.IsAny<ICmdletIO>(), It.IsAny<VideoCopyRequest>()))
            .Returns(Array.Empty<string>());

        using var ps = CreatePowerShell();
        AddCommonParameters(ps, CreateTempDirectory());

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Contains(errors, error =>
            error.Exception is InvalidOperationException exception
            && exception.Message.Contains("Video copying failed", StringComparison.Ordinal));
    }

    [Fact]
    public void InvokeSeriesProcessing_WhenSuccessful_CompletesWithoutCaptionExtractionWhenSkipped()
    {
        var seasonDir = SetupDirectoryStructure();
        SetupSeasonScan();
        var copiedPath = Path.Combine(seasonDir, "Show {tvdb 1} - s01e01.mkv");
        _seriesProcessingServiceMock
            .Setup(service => service.InvokeVideoCopy(It.IsAny<ICmdletIO>(), It.IsAny<VideoCopyRequest>()))
            .Returns([copiedPath]);

        using var ps = CreatePowerShell();
        AddCommonParameters(ps, CreateTempDirectory());
        ps.Commands.Commands[0].Parameters.Add("SkipCaptionExtraction", true);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Empty(results);
        _seriesProcessingServiceMock.Verify(
            service => service.InvokeCaptionExtractionPhase(
                It.IsAny<ICmdletIO>(),
                seasonDir,
                It.Is<IReadOnlyList<string>>(files => files.Single() == copiedPath),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void InvokeSeriesProcessing_WhenExtractChaptersSpecified_InvokesChapterExtractionPhase()
    {
        var seasonDir = SetupDirectoryStructure();
        SetupSeasonScan();
        var copiedPath = Path.Combine(seasonDir, "Show {tvdb 1} - s01e01.mkv");
        _seriesProcessingServiceMock
            .Setup(service => service.InvokeVideoCopy(It.IsAny<ICmdletIO>(), It.IsAny<VideoCopyRequest>()))
            .Returns([copiedPath]);
        _seriesProcessingServiceMock
            .Setup(service => service.InvokeChapterExtractionPhase(
                It.IsAny<ICmdletIO>(),
                seasonDir,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ProcessingPhaseStats(1, 0, 1));
        _seriesProcessingServiceMock
            .Setup(service => service.InvokeCaptionExtractionPhase(
                It.IsAny<ICmdletIO>(),
                seasonDir,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(new CaptionExtractionPhaseResult(0, 0, 1, []));

        using var ps = CreatePowerShell();
        AddCommonParameters(ps, CreateTempDirectory());
        ps.Commands.Commands[0].Parameters.Add("ExtractChapters", true);

        _ = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        _seriesProcessingServiceMock.Verify(
            service => service.InvokeChapterExtractionPhase(
                It.IsAny<ICmdletIO>(),
                seasonDir,
                It.Is<IReadOnlyList<string>>(files => files.Single() == copiedPath),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void InvokeSeriesProcessing_WhenWhatIfSpecified_DoesNotInvokeSeasonScan()
    {
        SetupDirectoryStructure();

        using var ps = CreatePowerShell();
        AddCommonParameters(ps, CreateTempDirectory());
        ps.Commands.Commands[0].Parameters.Add("WhatIf");

        _ = ps.Invoke();

        _seriesProcessingServiceMock.Verify(
            service => service.InvokeSeasonScan(
                It.IsAny<ICmdletIO>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("https://thetvdb.com/series/show/seasons/official", 2, "https://thetvdb.com/series/show/seasons/official/2")]
    [InlineData("https://thetvdb.com/series/show/seasons/official/2", 2, "https://thetvdb.com/series/show/seasons/official/2")]
    public void EnsureSeasonUrl_NormalizesSeasonSuffix(string input, int season, string expected)
    {
        var result = InvokeSeriesProcessingCommand.EnsureSeasonUrl(input, season);

        Assert.Equal(expected, result);
    }

    private string SetupDirectoryStructure()
    {
        var outputRoot = CreateTempDirectory();
        var seasonDir = Path.Combine(outputRoot, "Show", "Season 01");
        _seriesProcessingServiceMock
            .Setup(service => service.NewProcessingDirectoryStructure(
                It.IsAny<ICmdletIO>(),
                "Show",
                1,
                null,
                It.IsAny<string>()))
            .Returns(new ProcessingDirectoryStructure(outputRoot, seasonDir, []));
        return seasonDir;
    }

    private void SetupSeasonScan()
    {
        _seriesProcessingServiceMock
            .Setup(service => service.InvokeSeasonScan(It.IsAny<ICmdletIO>(), 1, null, null, It.IsAny<CancellationToken>()))
            .Returns([new TvDbEpisodeInfo("1", 1, "Pilot", 1)]);
    }

    private static void AddCommonParameters(PowerShell ps, string outputPath)
    {
        ps.AddCommand("Invoke-SeriesProcessing")
            .AddParameter("Title", "Show")
            .AddParameter("Season", 1)
            .AddParameter("InputPath", outputPath)
            .AddParameter("FilePatterns", "*.mkv")
            .AddParameter("OutputPath", outputPath)
            .AddParameter("MinimumFileSize", 0L)
            .AddParameter("Confirm", false);
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_SeriesProcessing_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private static PowerShell CreatePowerShell() =>
        PowerShellCmdletTestHost.Create<InvokeSeriesProcessingCommand>("Invoke-SeriesProcessing");
}
