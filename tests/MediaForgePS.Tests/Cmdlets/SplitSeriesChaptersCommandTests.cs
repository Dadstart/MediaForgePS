using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class SplitSeriesChaptersCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock;
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock;
    private readonly Mock<IExecutableService> _executableServiceMock;
    private readonly Mock<ISeriesProcessingService> _seriesProcessingServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<SplitSeriesChaptersCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

    public SplitSeriesChaptersCommandTests()
    {
        _pathResolverMock = new Mock<IPathResolver>();
        _mediaReaderServiceMock = new Mock<IMediaReaderService>();
        _executableServiceMock = new Mock<IExecutableService>();
        _seriesProcessingServiceMock = new Mock<ISeriesProcessingService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<SplitSeriesChaptersCommand>>();
        _debuggerServiceMock = new Mock<IDebuggerService>();

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));

        _executableServiceMock
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(null, null, 0));

        var services = new ServiceCollection();
        services.AddSingleton(_pathResolverMock.Object);
        services.AddSingleton(_mediaReaderServiceMock.Object);
        services.AddSingleton(_executableServiceMock.Object);
        services.AddSingleton(_seriesProcessingServiceMock.Object);
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        _serviceProvider = services.BuildServiceProvider();

        var moduleServicesType = typeof(ModuleServices);
        _providerField = moduleServicesType.GetField("_provider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        _initializedField = moduleServicesType.GetField("_initialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (_providerField != null)
            _providerField.SetValue(null, _serviceProvider);
        if (_initializedField != null)
            _initializedField.SetValue(null, true);
    }

    public void Dispose()
    {
        if (_providerField != null)
            _providerField.SetValue(null, null);
        if (_initializedField != null)
            _initializedField.SetValue(null, false);
    }

    [Fact]
    public void SplitSeriesChapters_WhenInputPathNotResolved_WritesError()
    {
        var inputPath = "C:\\nonexistent.mkv";
        string? resolvedPath = null;
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(false);
        _seriesProcessingServiceMock.Setup(s => s.InvokeSeasonScan(It.IsAny<PSCmdlet>(), 1, It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new[]
            {
                new TvDbEpisodeInfo("101", 1, "Episode 1", 1)
            });

        using var ps = CreatePowerShell();
        ps.AddCommand("Split-SeriesChapters")
            .AddParameter("InputFile", inputPath)
            .AddParameter("ChapterRanges", new object[] { new ChapterRange(1, 1) })
            .AddParameter("Title", "My Show")
            .AddParameter("TvDbSeriesUrl", "https://thetvdb.com/series/my-show")
            .AddParameter("Season", 1)
            .AddParameter("EpisodeStart", 1);

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
        _mediaReaderServiceMock.Verify(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SplitSeriesChapters_WhenNotEnoughTvDbEpisodes_WritesError()
    {
        _seriesProcessingServiceMock.Setup(s => s.InvokeSeasonScan(It.IsAny<PSCmdlet>(), 1, It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new[]
            {
                new TvDbEpisodeInfo("101", 1, "Episode 1", 1)
            });

        using var ps = CreatePowerShell();
        ps.AddCommand("Split-SeriesChapters")
            .AddParameter("InputFile", "C:\\input.mkv")
            .AddParameter("ChapterRanges", new object[]
            {
                new ChapterRange(1, 1),
                new ChapterRange(2, 2)
            })
            .AddParameter("Title", "My Show")
            .AddParameter("TvDbSeriesUrl", "https://thetvdb.com/series/my-show")
            .AddParameter("Season", 1)
            .AddParameter("EpisodeStart", 1);

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
        _mediaReaderServiceMock.Verify(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _executableServiceMock.Verify(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SplitSeriesChapters_WithValidInput_CallsFfmpegAndWritesOutputPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SplitSeriesChapters_" + Guid.NewGuid().ToString("N"));
        var inputPath = Path.Combine(tempDir, "input.mkv");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(inputPath, "placeholder");

            string? resolvedPath = inputPath;
            _pathResolverMock.Setup(p => p.TryResolveInputPath(It.IsAny<string>(), out resolvedPath))
                .Callback(new TryResolveInputPathCallback((string p, out string r) => r = p))
                .Returns(true);

            _seriesProcessingServiceMock.Setup(s => s.InvokeSeasonScan(It.IsAny<PSCmdlet>(), 1, It.IsAny<string?>(), It.IsAny<string?>()))
                .Returns(new[]
                {
                    new TvDbEpisodeInfo("123456", 1, "Episode 1", 1)
                });

            var chapters = new[]
            {
                new MediaChapter(0, 0, 100, new Dictionary<string, string>(), null, "")
            };
            var mediaFile = new MediaFile(
                inputPath,
                new MediaFormat(inputPath, 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
                chapters,
                Array.Empty<MediaStream>(),
                "{}");
            _mediaReaderServiceMock.Setup(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mediaFile);

            using var ps = CreatePowerShell();
            ps.AddCommand("Split-SeriesChapters")
                .AddParameter("InputFile", inputPath)
                .AddParameter("ChapterRanges", new object[] { new ChapterRange(1, 1) })
                .AddParameter("Title", "My Show")
                .AddParameter("TvDbSeriesUrl", "https://thetvdb.com/series/my-show")
                .AddParameter("Season", 1)
                .AddParameter("EpisodeStart", 1);

            var results = ps.Invoke().ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.Single(results);
            var expectedOutputPath = Path.Combine(tempDir, "My Show {tvdb 123456} S01E01.mkv");
            Assert.Equal(expectedOutputPath, results[0].BaseObject);

            _executableServiceMock.Verify(
                e => e.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { File.Delete(inputPath); } catch { }
                try { Directory.Delete(tempDir); } catch { }
            }
        }
    }

    private static PowerShell CreatePowerShell()
    {
        var asm = typeof(SplitSeriesChaptersCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Split-SeriesChapters", typeof(SplitSeriesChaptersCommand), null));
        return PowerShell.Create(initialSessionState);
    }

    private delegate void TryResolveInputPathCallback(string path, out string resolvedPath);
}
