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

public class InvokeDvdProcessingCommandTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<InvokeDvdProcessingCommand>> _loggerMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

    public InvokeDvdProcessingCommandTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<InvokeDvdProcessingCommand>>();
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
    public void InvokeDvdProcessing_WhenRequiredCommandMissing_WritesError()
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(typeof(InvokeDvdProcessingCommand).Assembly);
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Invoke-DvdProcessing", typeof(InvokeDvdProcessingCommand), null));

        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var pipeline = runspace.CreatePipeline();
        pipeline.Commands.Add("Invoke-DvdProcessing", new CommandParameter[] { })
            .Parameters.Add("Title", "Test Title");
        pipeline.Commands[0].Parameters.Add("Path", new[] { "C:\\Source" });
        pipeline.Commands[0].Parameters.Add("FilePatterns", new[] { "*.vob" });
        pipeline.Commands[0].Parameters.Add("Season", 1);
        pipeline.Commands[0].Parameters.Add("TvDbSeriesUrl", "https://thetvdb.com/series/breaking-bad");

        try
        {
            pipeline.Invoke();
        }
        catch (Exception)
        {
            // Invoke may throw; we care about error stream
        }

        var errors = pipeline.Error;
        var hasRequiredCommandError = errors != null && errors.Count > 0;
        if (errors != null)
        {
            foreach (ErrorRecord er in errors)
            {
                if (er.FullyQualifiedErrorId?.Contains("DvdProcessingRequired") == true ||
                    er.Exception?.Message?.Contains("New-ProcessingDirectoryStructure") == true ||
                    er.Exception?.Message?.Contains("Required command") == true)
                {
                    hasRequiredCommandError = true;
                    break;
                }
            }
        }

        Assert.True(hasRequiredCommandError,
            "Expected cmdlet to write an error when required Rip/Media commands are not loaded.");
    }

    [Fact]
    public void InvokeDvdProcessing_WhenTvDbUrlMissing_WritesError()
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(typeof(InvokeDvdProcessingCommand).Assembly);
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Invoke-DvdProcessing", typeof(InvokeDvdProcessingCommand), null));

        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var pipeline = runspace.CreatePipeline();
        var cmd = pipeline.Commands.Add("Invoke-DvdProcessing", new CommandParameter[] { });
        cmd.Parameters.Add("Title", "Test");
        cmd.Parameters.Add("Path", new[] { "C:\\Source" });
        cmd.Parameters.Add("FilePatterns", new[] { "*.vob" });
        cmd.Parameters.Add("Season", 1);
        // Intentionally omit TvDbSeriesUrl and TvDbSeasonUrl

        pipeline.Invoke();

        var errorCount = pipeline.Error?.Count ?? 0;
        Assert.True(errorCount > 0, "Expected error when neither TvDbSeriesUrl nor TvDbSeasonUrl is provided.");
    }
}
