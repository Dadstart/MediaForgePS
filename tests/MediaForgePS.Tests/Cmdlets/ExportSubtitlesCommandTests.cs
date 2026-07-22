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
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ExportSubtitlesCommandTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<ExportSubtitlesCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly Mock<IMediaReaderService> _mediaReaderMock;
    private readonly Mock<IExecutableService> _executableMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

    public ExportSubtitlesCommandTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<ExportSubtitlesCommand>>();
        _debuggerServiceMock = new Mock<IDebuggerService>();

        var pathResolverLoggerMock = new Mock<ILogger<PathResolver>>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns((string name) => name?.Contains("PathResolver") == true ? pathResolverLoggerMock.Object : _loggerMock.Object);
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));

        _mediaReaderMock = new Mock<IMediaReaderService>();
        _executableMock = new Mock<IExecutableService>();
        var ocrConverterMock = new Mock<IImageSubtitleOcrConverter>();
        ocrConverterMock.SetupGet(c => c.IsAvailable).Returns(true);
        ocrConverterMock.SetupGet(c => c.ExpectedTessDataDescription).Returns("tessdata expected");
        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton<IMediaReaderService>(_mediaReaderMock.Object);
        services.AddSingleton<IExecutableService>(_executableMock.Object);
        services.AddSingleton<IImageSubtitleOcrConverter>(ocrConverterMock.Object);
        services.AddSingleton<ILogger<PathResolver>>(pathResolverLoggerMock.Object);
        services.AddSingleton<IPathResolver, PathResolver>();
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
    public void ExportSubtitles_WhenPathHasNoMkvFiles_WritesWarning()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ExportSubtitles_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(emptyDir);
            var asm = typeof(ExportSubtitlesCommand).Assembly;
            var initialSessionState = InitialSessionState.CreateDefault();
            initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
            initialSessionState.Commands.Add(new SessionStateCmdletEntry("Export-Subtitles", typeof(ExportSubtitlesCommand), null));

            using var ps = System.Management.Automation.PowerShell.Create(initialSessionState);
            ps.AddCommand("Export-Subtitles").AddParameter("InputPath", new[] { emptyDir });

            var results = ps.Invoke();
            var errors = ps.Streams.Error.ReadAll();
            var warnings = ps.Streams.Warning.ReadAll();

            Assert.Empty(results);
            Assert.Empty(errors);
            Assert.NotEmpty(warnings);
        }
        finally
        {
            if (Directory.Exists(emptyDir))
                Directory.Delete(emptyDir);
        }
    }

    [Fact]
    public void ExportSubtitles_WhenPathDoesNotExist_WritesError()
    {
        var asm = typeof(ExportSubtitlesCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Export-Subtitles", typeof(ExportSubtitlesCommand), null));

        using var ps = System.Management.Automation.PowerShell.Create(initialSessionState);
        ps.AddCommand("Export-Subtitles").AddParameter("InputPath", new[] { "C:\\Nonexistent\\path\\file.mkv" });

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ExportSubtitles_UsesOcrParameter()
    {
        var cmdlet = new ExportSubtitlesCommand();

        Assert.Equal(SubtitleOcrMode.Auto, cmdlet.Ocr);
        Assert.NotNull(typeof(ExportSubtitlesCommand).GetProperty(nameof(ExportSubtitlesCommand.Ocr)));
        Assert.Null(typeof(ExportSubtitlesCommand).GetProperty("SkipOcr"));
    }

    [Fact]
    public void ExportSubtitles_WithoutOcr_SkipsRepairWorkflowForExtractedSrt()
    {
        using var tempDir = new TemporaryDirectory();
        var mediaPath = Path.Combine(tempDir.Path, "movie.mkv");
        File.WriteAllText(mediaPath, "not-a-real-media-file");

        _mediaReaderMock
            .Setup(service => service.GetMediaFileAsync(mediaPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mediaPath, "subrip"));

        _executableMock
            .Setup(service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, string.Empty, 0));

        var asm = typeof(ExportSubtitlesCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Export-Subtitles", typeof(ExportSubtitlesCommand), null));

        using var ps = System.Management.Automation.PowerShell.Create(initialSessionState);
        ps.AddCommand("Export-Subtitles")
            .AddParameter("InputPath", new[] { mediaPath })
            .AddParameter("Ocr", SubtitleOcrMode.Skip);

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.NotEmpty(results);
        var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results.OfType<SubtitleProcessingResult>()));
        Assert.Equal(1, result.ExtractedCount);
        Assert.Equal(0, result.ConvertedCount);
    }

    [Fact]
    public void ExportSubtitles_WithOnlyNativeSrt_DoesNotRepair()
    {
        using var tempDir = new TemporaryDirectory();
        var mediaPath = Path.Combine(tempDir.Path, "movie.mkv");
        File.WriteAllText(mediaPath, "not-a-real-media-file");

        _mediaReaderMock
            .Setup(service => service.GetMediaFileAsync(mediaPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMediaFile(mediaPath, "subrip"));

        _executableMock
            .Setup(service => service.ExecuteAsync("ffmpeg", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, string.Empty, 0));

        var asm = typeof(ExportSubtitlesCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Export-Subtitles", typeof(ExportSubtitlesCommand), null));

        using var ps = System.Management.Automation.PowerShell.Create(initialSessionState);
        ps.AddCommand("Export-Subtitles")
            .AddParameter("InputPath", new[] { mediaPath });

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results.OfType<SubtitleProcessingResult>()));
        Assert.Equal(1, result.ExtractedCount);
        Assert.Equal(0, result.ConvertedCount);
    }

    private static MediaFile CreateMediaFile(string mediaPath, string subtitleCodec)
    {
        return new MediaFile(
            mediaPath,
            new MediaFormat(
                mediaPath,
                1,
                "matroska",
                "Matroska",
                0,
                1,
                1024,
                1024,
                new Dictionary<string, string>()),
            [],
            [new MediaStream("subtitle", 2, subtitleCodec, string.Empty, string.Empty, new Dictionary<string, string>(), TimeSpan.Zero, "eng")],
            string.Empty);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MediaForgePS_ExportSubtitles_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
