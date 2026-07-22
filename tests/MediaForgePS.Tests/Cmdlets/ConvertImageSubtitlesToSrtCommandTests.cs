using System;
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

public class ConvertImageSubtitlesToSrtCommandTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly Mock<IImageSubtitleOcrConverter> _ocrConverterMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

    public ConvertImageSubtitlesToSrtCommandTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger<ConvertImageSubtitlesToSrtCommand>>();
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
        _ocrConverterMock.SetupGet(c => c.IsAvailable).Returns(true);
        _ocrConverterMock.SetupGet(c => c.ExpectedTessDataDescription).Returns("tessdata expected");
        _ocrConverterMock
            .Setup(c => c.ConvertToSrt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, outputPath, _) =>
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                if (!File.Exists(outputPath))
                    File.WriteAllText(outputPath, "1\n00:00:00,000 --> 00:00:01,000\n\n");
            });

        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton<ILogger<PathResolver>>(pathResolverLoggerMock.Object);
        services.AddSingleton<IPathResolver, PathResolver>();
        services.AddSingleton<IImageSubtitleOcrConverter>(_ocrConverterMock.Object);
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

    private static (InitialSessionState sessionState, string cmdletName) CreateSessionState()
    {
        var asm = typeof(ConvertImageSubtitlesToSrtCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Convert-ImageSubtitlesToSrt", typeof(ConvertImageSubtitlesToSrtCommand), null));
        initialSessionState.Commands.Add(new SessionStateAliasEntry("Convert-SupToSrt", "Convert-ImageSubtitlesToSrt"));
        return (initialSessionState, "Convert-ImageSubtitlesToSrt");
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenNoInputPathsProvided_WritesWarning()
    {
        var (initialSessionState, cmdletName) = CreateSessionState();
        using var ps = PowerShell.Create(initialSessionState);
        ps.AddCommand(cmdletName).AddParameter("InputPath", new[] { "   ", "\t" });

        ps.Invoke();
        var warnings = ps.Streams.Warning.ReadAll();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Single(warnings);
        Assert.Contains("No input path", warnings[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenPathDoesNotExist_WritesError()
    {
        var (initialSessionState, cmdletName) = CreateSessionState();
        using var ps = PowerShell.Create(initialSessionState);
        ps.AddCommand(cmdletName).AddParameter("InputPath", "C:\\Nonexistent\\file.sup");

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenTesseractDataNotFound_WritesError()
    {
        _ocrConverterMock.SetupGet(c => c.IsAvailable).Returns(false);

        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ConvertImageSubtitlesToSrt_" + Guid.NewGuid().ToString("N"));
        var supPath = Path.Combine(tempDir, "test.sup");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(supPath, Array.Empty<byte>());

            var (initialSessionState, cmdletName) = CreateSessionState();
            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand(cmdletName).AddParameter("InputPath", supPath);

            ps.Invoke();
            var errors = ps.Streams.Error.ReadAll();

            Assert.NotEmpty(errors);
            Assert.True(errors.Any(e => e.FullyQualifiedErrorId.Contains("TesseractDataNotFound", StringComparison.Ordinal)),
                "Expected TesseractDataNotFound error.");
        }
        finally
        {
            if (File.Exists(supPath))
                File.Delete(supPath);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenNoResolvablePathsExist_WritesWarningOrErrorAndNoOutput()
    {
        var pathThatDoesNotExist = Path.Combine(Path.GetTempPath(), "MediaForgePS_NoExist_" + Guid.NewGuid().ToString("N"));

        var (initialSessionState, cmdletName) = CreateSessionState();
        using var ps = PowerShell.Create(initialSessionState);
        ps.AddCommand(cmdletName).AddParameter("InputPath", pathThatDoesNotExist);

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var warnings = ps.Streams.Warning.ReadAll();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.True(warnings.Count > 0 || errors.Count > 0, "Expected either warning (no resolvable paths) or error (path not found).");
        if (warnings.Count > 0)
            Assert.Contains("No existing file or directory", warnings[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenSingleFileSucceeds_OutputsSrtPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ConvertImageSubtitlesToSrt_" + Guid.NewGuid().ToString("N"));
        var supPath = Path.Combine(tempDir, "test.sup");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(supPath, Array.Empty<byte>());

            var (initialSessionState, cmdletName) = CreateSessionState();
            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand(cmdletName).AddParameter("InputPath", supPath);

            var results = ps.Invoke().Select(p => p.BaseObject).ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.Single(results);
            var result = Assert.IsType<SubtitleProcessingResult>(results[0]);
            Assert.Equal(0, result.ExtractedCount);
            Assert.Equal(1, result.ConvertedCount);
            Assert.Equal(Path.ChangeExtension(supPath, "srt"), Assert.Single(result.ConvertedPaths));
            Assert.False(File.Exists(supPath));
        }
        finally
        {
            var srtPath = Path.ChangeExtension(supPath, "srt");
            if (File.Exists(srtPath))
                File.Delete(srtPath);
            if (File.Exists(supPath))
                File.Delete(supPath);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenOcrFails_WritesError()
    {
        _ocrConverterMock
            .Setup(c => c.ConvertToSrt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("OCR failed"));

        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ConvertImageSubtitlesToSrt_" + Guid.NewGuid().ToString("N"));
        var supPath = Path.Combine(tempDir, "test.sup");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(supPath, Array.Empty<byte>());

            var (initialSessionState, cmdletName) = CreateSessionState();
            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand(cmdletName).AddParameter("InputPath", supPath);

            var results = ps.Invoke().Select(p => p.BaseObject).ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.NotEmpty(errors);
            var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results));
            Assert.Equal(0, result.ConvertedCount);
            Assert.True(File.Exists(supPath));
        }
        finally
        {
            if (File.Exists(supPath))
                File.Delete(supPath);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenOutputPathResolutionFails_WritesError()
    {
        var pathResolverMock = new Mock<IPathResolver>();
        var resolvedPath = string.Empty;
        pathResolverMock
            .Setup(r => r.TryResolveOutputPath(It.IsAny<string>(), out resolvedPath))
            .Returns(false);

        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton(pathResolverMock.Object);
        services.AddSingleton<IImageSubtitleOcrConverter>(_ocrConverterMock.Object);
        var customProvider = services.BuildServiceProvider();

        var moduleServicesType = typeof(ModuleServices);
        var providerField = moduleServicesType.GetField("_provider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ConvertImageSubtitlesToSrt_" + Guid.NewGuid().ToString("N"));
        var supPath = Path.Combine(tempDir, "test.sup");
        var customOutput = Path.Combine(tempDir, "custom.srt");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(supPath, Array.Empty<byte>());

            if (providerField != null)
                providerField.SetValue(null, customProvider);

            var (initialSessionState, cmdletName) = CreateSessionState();
            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand(cmdletName).AddParameter("InputPath", supPath).AddParameter("OutputPath", customOutput);

            var results = ps.Invoke().Select(p => p.BaseObject).ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.NotEmpty(errors);
            var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results));
            Assert.Equal(0, result.ConvertedCount);
            Assert.True(File.Exists(supPath));
        }
        finally
        {
            if (providerField != null)
                providerField.SetValue(null, _serviceProvider);
            if (File.Exists(supPath))
                File.Delete(supPath);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenCustomOutputPath_WritesSrtToOutputPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ConvertImageSubtitlesToSrt_" + Guid.NewGuid().ToString("N"));
        var supPath = Path.Combine(tempDir, "test.sup");
        var customOutput = Path.Combine(tempDir, "output", "custom.srt");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(supPath, Array.Empty<byte>());

            var (initialSessionState, cmdletName) = CreateSessionState();
            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand(cmdletName).AddParameter("InputPath", supPath).AddParameter("OutputPath", customOutput);

            var results = ps.Invoke().Select(p => p.BaseObject).ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.Single(results);
            var result = Assert.IsType<SubtitleProcessingResult>(results[0]);
            Assert.Equal(1, result.ConvertedCount);
            Assert.Equal(customOutput, Assert.Single(result.ConvertedPaths));
            Assert.True(File.Exists(customOutput));
            Assert.False(File.Exists(supPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WhenDirectoryHasNoSupFiles_OutputsEmptyResult()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ConvertImageSubtitlesToSrt_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);

            var (initialSessionState, cmdletName) = CreateSessionState();
            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand(cmdletName).AddParameter("InputPath", tempDir);

            var results = ps.Invoke().Select(p => p.BaseObject).ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.Single(results);
            var result = Assert.IsType<SubtitleProcessingResult>(results[0]);
            Assert.Equal(0, result.ExtractedCount);
            Assert.Equal(0, result.ConvertedCount);
            Assert.Empty(result.ConvertedPaths);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_WithRecurse_FindsSupInSubdirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ConvertImageSubtitlesToSrt_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "sub");
        var supPath = Path.Combine(subDir, "nested.sup");
        try
        {
            Directory.CreateDirectory(subDir);
            File.WriteAllBytes(supPath, Array.Empty<byte>());

            var (initialSessionState, cmdletName) = CreateSessionState();
            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand(cmdletName).AddParameter("InputPath", tempDir).AddParameter("Recurse");

            var results = ps.Invoke().Select(p => p.BaseObject).ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.Single(results);
            var result = Assert.IsType<SubtitleProcessingResult>(results[0]);
            Assert.Equal(1, result.ConvertedCount);
            Assert.Equal(Path.ChangeExtension(supPath, "srt"), Assert.Single(result.ConvertedPaths));
            Assert.False(File.Exists(supPath));
        }
        finally
        {
            var srtPath = Path.ChangeExtension(supPath, "srt");
            if (File.Exists(srtPath))
                File.Delete(srtPath);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConvertImageSubtitlesToSrt_Alias_ResolvesToConvertImageSubtitlesToSrt()
    {
        var (initialSessionState, _) = CreateSessionState();
        using var ps = PowerShell.Create(initialSessionState);
        ps.AddCommand("Convert-SupToSrt").AddParameter("InputPath", new[] { "  " });

        ps.Invoke();
        var warnings = ps.Streams.Warning.ReadAll();

        Assert.Single(warnings);
        Assert.Contains("No input path", warnings[0].Message, StringComparison.OrdinalIgnoreCase);
    }
}
