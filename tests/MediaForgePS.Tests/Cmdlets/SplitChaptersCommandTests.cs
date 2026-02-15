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
using Dadstart.Labs.MediaForge.Services.System;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

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
    public void SplitChapters_WhenInputPathNotResolved_WritesError()
    {
        var inputPath = "C:\\nonexistent.mkv";
        string? resolvedPath = null;
        _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
            .Returns(false);

        var asm = typeof(SplitChaptersCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Split-Chapters", typeof(SplitChaptersCommand), null));

        using var ps = PowerShell.Create(initialSessionState);
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

        var asm = typeof(SplitChaptersCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Split-Chapters", typeof(SplitChaptersCommand), null));

        using var ps = PowerShell.Create(initialSessionState);
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
            _pathResolverMock.Setup(p => p.TryResolveInputPath(inputPath, out resolvedPath))
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
            _mediaReaderServiceMock.Setup(m => m.GetMediaFileAsync(inputPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mediaFile);

            var asm = typeof(SplitChaptersCommand).Assembly;
            var initialSessionState = InitialSessionState.CreateDefault();
            initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
            initialSessionState.Commands.Add(new SessionStateCmdletEntry("Split-Chapters", typeof(SplitChaptersCommand), null));

            using var ps = PowerShell.Create(initialSessionState);
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
}
