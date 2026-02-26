using System;
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

public class InvokeSubtitleOcrRepairCommandTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Reflection.FieldInfo? _providerField;
    private readonly System.Reflection.FieldInfo? _initializedField;

    public InvokeSubtitleOcrRepairCommandTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger<InvokeSubtitleOcrRepairCommand>>();
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

        var executableMock = new Mock<IExecutableService>();
        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton<IExecutableService>(executableMock.Object);
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
    public void InvokeSubtitleOcrRepair_WhenPathDoesNotExist_WritesError()
    {
        var asm = typeof(InvokeSubtitleOcrRepairCommand).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Invoke-SubtitleOcrRepair", typeof(InvokeSubtitleOcrRepairCommand), null));

        using var ps = PowerShell.Create(initialSessionState);
        ps.AddCommand("Invoke-SubtitleOcrRepair").AddParameter("InputPath", new[] { "C:\\Nonexistent\\path.sup" });

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void InvokeSubtitleOcrRepair_WhenDirectoryHasNoSubtitleFiles_WritesWarning()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_InvokeSubtitleOcrRepair_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(emptyDir);
            var asm = typeof(InvokeSubtitleOcrRepairCommand).Assembly;
            var initialSessionState = InitialSessionState.CreateDefault();
            initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName, asm.Location));
            initialSessionState.Commands.Add(new SessionStateCmdletEntry("Invoke-SubtitleOcrRepair", typeof(InvokeSubtitleOcrRepairCommand), null));

            using var ps = PowerShell.Create(initialSessionState);
            ps.AddCommand("Invoke-SubtitleOcrRepair").AddParameter("InputPath", new[] { emptyDir });

            ps.Invoke();
            var warnings = ps.Streams.Warning.ReadAll();

            Assert.NotEmpty(warnings);
        }
        finally
        {
            if (Directory.Exists(emptyDir))
                Directory.Delete(emptyDir);
        }
    }
}
