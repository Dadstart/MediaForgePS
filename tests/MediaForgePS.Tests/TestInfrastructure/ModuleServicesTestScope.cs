using System;
using System.Reflection;
using Dadstart.Labs.MediaForge.Services;

namespace Dadstart.Labs.MediaForge.Tests.TestInfrastructure;

/// <summary>
/// Temporarily injects a service provider into ModuleServices for cmdlet tests.
/// </summary>
public sealed class ModuleServicesTestScope : IDisposable
{
    private static readonly FieldInfo? _providerField = typeof(ModuleServices).GetField("_provider", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo? _initializedField = typeof(ModuleServices).GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo? _disposedField = typeof(ModuleServices).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo? _disposeRequestedField = typeof(ModuleServices).GetField("_disposeRequested", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo? _inFlightCmdletCountField = typeof(ModuleServices).GetField("_inFlightCmdletCount", BindingFlags.NonPublic | BindingFlags.Static);

    public ModuleServicesTestScope(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ModuleServices.ResetForTesting();
        _providerField?.SetValue(null, provider);
        _initializedField?.SetValue(null, true);
        _disposedField?.SetValue(null, false);
        _disposeRequestedField?.SetValue(null, false);
        _inFlightCmdletCountField?.SetValue(null, 0);
    }

    public void Dispose()
    {
        ModuleServices.ResetForTesting();
    }
}
