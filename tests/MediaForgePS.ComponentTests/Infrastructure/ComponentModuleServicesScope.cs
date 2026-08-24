using System;
using System.Reflection;
using Dadstart.Labs.MediaForge.Services;

namespace Dadstart.Labs.MediaForge.ComponentTests.Infrastructure;

/// <summary>
/// Temporarily injects a service provider into <see cref="ModuleServices"/> for component tests.
/// </summary>
public sealed class ComponentModuleServicesScope : IDisposable
{
    private static readonly FieldInfo? _providerField =
        typeof(ModuleServices).GetField("_provider", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? _initializedField =
        typeof(ModuleServices).GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);

    public ComponentModuleServicesScope(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ModuleServices.ResetForTesting();
        _providerField?.SetValue(null, provider);
        _initializedField?.SetValue(null, true);
    }

    public void Dispose()
    {
        ModuleServices.ResetForTesting();
    }
}
