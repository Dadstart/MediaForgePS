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

    public ModuleServicesTestScope(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providerField?.SetValue(null, provider);
        _initializedField?.SetValue(null, true);
    }

    public void Dispose()
    {
        _providerField?.SetValue(null, null);
        _initializedField?.SetValue(null, false);
    }
}
