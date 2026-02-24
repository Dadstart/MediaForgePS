using System;
using System.Collections.ObjectModel;
using System.IO;
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

public class ExportSubtitlesCommandTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<ExportSubtitlesCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
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

        var mediaReaderMock = new Mock<IMediaReaderService>();
        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton<IMediaReaderService>(mediaReaderMock.Object);
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
            initialSessionState.Commands.Add(new SessionStateCmdletEntry("Export-AllSubtitles", typeof(ExportSubtitlesCommand), null));

            using var ps = System.Management.Automation.PowerShell.Create(initialSessionState);
            ps.AddCommand("Export-AllSubtitles").AddParameter("InputPath", new[] { emptyDir });

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
    public void ExportSubtitles_Alias_ResolvesToExportAllSubtitles()
    {
        var asm = typeof(ExportSubtitlesCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Export-AllSubtitles", typeof(ExportSubtitlesCommand), null));
        initialSessionState.Commands.Add(new SessionStateAliasEntry("Export-Subtitles", "Export-AllSubtitles"));

        using var ps = System.Management.Automation.PowerShell.Create(initialSessionState);
        ps.AddCommand("Export-Subtitles").AddParameter("InputPath", new[] { "C:\\NonexistentFolder" });

        // Should not throw; may write error for invalid path
        ps.Invoke();
    }
}
