using System;
using System.Collections.ObjectModel;
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

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
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
    public void ExportSubtitles_WhenNoPathProvided_WritesWarning()
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(typeof(ExportSubtitlesCommand).Assembly);
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Export-AllSubtitles", typeof(ExportSubtitlesCommand), null));

        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var pipeline = runspace.CreatePipeline();
        pipeline.Commands.Add("Export-AllSubtitles");
        pipeline.Commands[0].Parameters.Add("InputPath", Array.Empty<string>());

        var results = pipeline.Invoke();
        var errors = pipeline.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.Empty(errors);
        var warnings = pipeline.Streams.Warning.ReadAll();
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void ExportSubtitles_Alias_ResolvesToExportAllSubtitles()
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(typeof(ExportSubtitlesCommand).Assembly);
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Export-AllSubtitles", typeof(ExportSubtitlesCommand), null));
        initialSessionState.Commands.Add(new SessionStateAliasEntry("Export-Subtitles", "Export-AllSubtitles"));

        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var pipeline = runspace.CreatePipeline();
        pipeline.Commands.Add("Export-Subtitles");
        pipeline.Commands[0].Parameters.Add("InputPath", new[] { "C:\\NonexistentFolder" });

        // Should not throw; may write error for invalid path
        pipeline.Invoke();
    }
}
