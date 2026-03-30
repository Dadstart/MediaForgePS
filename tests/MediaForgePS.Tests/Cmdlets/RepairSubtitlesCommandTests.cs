using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class RepairSubtitlesCommandTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

    public RepairSubtitlesCommandTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger<RepairSubtitlesCommand>>();
        var pathResolverLoggerMock = new Mock<ILogger<PathResolver>>();
        _debuggerServiceMock = new Mock<IDebuggerService>();

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns((string name) =>
            {
                if (name?.Contains("PathResolver") == true)
                    return pathResolverLoggerMock.Object;
                return loggerMock.Object;
            });
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
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
    public void RepairSubtitles_WhenSingleFile_FixesContentAndOutputsPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_RepairSubtitles_" + Guid.NewGuid().ToString("N"));
        var srtPath = Path.Combine(tempDir, "test.srt");
        try
        {
            Directory.CreateDirectory(tempDir);
            var content = "1\n00:00:01,000 --> 00:00:02,000\nSong J plays.\n\n";
            File.WriteAllText(srtPath, content);

            var asm = typeof(RepairSubtitlesCommand).Assembly;
            var initialSessionState = InitialSessionState.CreateDefault();
            initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
            initialSessionState.Commands.Add(new SessionStateCmdletEntry("Repair-Subtitles", typeof(RepairSubtitlesCommand), null));

            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand("Repair-Subtitles").AddParameter("InputPath", srtPath);

            ps.Invoke();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);

            var written = File.ReadAllText(srtPath);
            Assert.Contains("♪", written);
            Assert.Contains("Song ♪ plays.", written);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void RepairSubtitles_WhenSingleFileWithOutputPath_WritesToOutputPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_RepairSubtitles_" + Guid.NewGuid().ToString("N"));
        var inputPath = Path.Combine(tempDir, "input.srt");
        var outputPath = Path.Combine(tempDir, "output.srt");

        try
        {
            Directory.CreateDirectory(tempDir);
            var content = "1\n00:00:01,000 --> 00:00:02,000\nSong J plays.\n\n";
            File.WriteAllText(inputPath, content);

            var asm = typeof(RepairSubtitlesCommand).Assembly;
            var initialSessionState = InitialSessionState.CreateDefault();
            initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
            initialSessionState.Commands.Add(new SessionStateCmdletEntry("Repair-Subtitles", typeof(RepairSubtitlesCommand), null));

            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand("Repair-Subtitles")
                .AddParameter("InputPath", inputPath)
                .AddParameter("OutputPath", outputPath);

            ps.Invoke();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("Song ♪ plays.", File.ReadAllText(outputPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
