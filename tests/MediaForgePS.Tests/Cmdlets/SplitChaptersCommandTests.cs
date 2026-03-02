using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class SplitChaptersCommandTests : IDisposable
{
    private readonly Mock<IPathResolver> _pathResolverMock;
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock;
    private readonly Mock<IExecutableService> _executableServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<SplitChaptersCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public SplitChaptersCommandTests()
    {
        _pathResolverMock = new Mock<IPathResolver>();
        _mediaReaderServiceMock = new Mock<IMediaReaderService>();
        _executableServiceMock = new Mock<IExecutableService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<SplitChaptersCommand>>();
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
    public void SplitChapters_WhenInputPathNotResolved_WritesError()
    {
        var inputPath = "C:\\nonexistent.mkv";
        string? resolvedPath = null;
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(false);

        using var ps = CreatePowerShell();
        ps.AddCommand("Split-Chapters")
            .AddParameter("InputFile", inputPath)
            .AddParameter("ChapterRanges", new object[] { new ChapterRange(1, 1) });

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
        _mediaReaderServiceMock.Verify(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SplitChapters_WhenNoChaptersInFile_WritesError()
    {
        var inputPath = "C:\\input.mkv";
        var resolvedPath = "C:\\input.mkv";
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(true);
        _mediaReaderServiceMock.Setup(m => m.GetMediaFileAsync(resolvedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFile?)null);

        using var ps = CreatePowerShell();
        ps.AddCommand("Split-Chapters")
            .AddParameter("InputFile", inputPath)
            .AddParameter("ChapterRanges", new object[] { new ChapterRange(1, 1) });

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
        _executableServiceMock.Verify(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SplitChapters_WithValidChapters_CallsFfmpegAndWritesOutputPaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SplitChapters_" + Guid.NewGuid().ToString("N"));
        var inputPath = Path.Combine(tempDir, "input.mkv");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(inputPath, "placeholder");

            string? resolvedPath = inputPath;
            _pathResolverMock.Setup(p => p.TryResolveInputPath(It.IsAny<string>(), out resolvedPath))
                .Callback(new TryResolveInputPathCallback((string p, out string r) => r = p))
                .Returns(true);

            var chapters = new[]
            {
                new MediaChapter(0, 0, 100, new Dictionary<string, string>(), null, ""),
                new MediaChapter(1, 100, 200, new Dictionary<string, string>(), null, "")
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
            ps.AddCommand("Split-Chapters")
                .AddParameter("InputFile", inputPath)
                .AddParameter("ChapterRanges", new object[] { new ChapterRange(1, 1, "Episode1") });

            var results = ps.Invoke().ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.Single(results);
            var expectedOutputPath = Path.Combine(tempDir, "Episode1.mkv");
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

    [Fact]
    public void SplitChapters_WithAllChapters_SplitsEveryChapterIntoOwnFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SplitChapters_" + Guid.NewGuid().ToString("N"));
        var inputPath = Path.Combine(tempDir, "input.mkv");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(inputPath, "placeholder");

            string? resolvedPath = inputPath;
            _pathResolverMock.Setup(p => p.TryResolveInputPath(It.IsAny<string>(), out resolvedPath))
                .Callback(new TryResolveInputPathCallback((string p, out string r) => r = p))
                .Returns(true);

            var chapters = new[]
            {
                new MediaChapter(0, 0, 100, new Dictionary<string, string>(), null, ""),
                new MediaChapter(1, 100, 200, new Dictionary<string, string>(), null, ""),
                new MediaChapter(2, 200, 300, new Dictionary<string, string>(), null, "")
            };
            var mediaFile = new MediaFile(
                inputPath,
                new MediaFormat(inputPath, 1, "matroska", "Matroska", 0, 300, 1000, 1000, new Dictionary<string, string>()),
                chapters,
                Array.Empty<MediaStream>(),
                "{}");
            _mediaReaderServiceMock.Setup(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mediaFile);

            using var ps = CreatePowerShell();
            ps.AddCommand("Split-Chapters")
                .AddParameter("InputFile", inputPath)
                .AddParameter("AllChapters", true);

            var results = ps.Invoke().ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.Equal(3, results.Count);
            Assert.Equal(Path.Combine(tempDir, "input.split-01.mkv"), results[0].BaseObject);
            Assert.Equal(Path.Combine(tempDir, "input.split-02.mkv"), results[1].BaseObject);
            Assert.Equal(Path.Combine(tempDir, "input.split-03.mkv"), results[2].BaseObject);

            _executableServiceMock.Verify(
                e => e.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
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

    private static PowerShell CreatePowerShell() => PowerShellCmdletTestHost.Create<SplitChaptersCommand>("Split-Chapters");

    private delegate void TryResolveInputPathCallback(string path, out string resolvedPath);
}
