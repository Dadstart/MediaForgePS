using System.Reflection;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Module;

public sealed class ModuleInitializerTests : IDisposable
{
    public ModuleInitializerTests()
    {
        ModuleServices.ResetForTesting();
    }

    public void Dispose()
    {
        ModuleServices.ResetForTesting();
    }

    [Fact]
    public void Initialize_EnsuresModuleServicesCanResolveServices()
    {
        ModuleInitializer.Initialize();

        var platformService = ModuleServices.GetRequiredService<IPlatformService>();

        Assert.NotNull(platformService);
    }

    [Fact]
    public void Cleanup_DisposesModuleServices()
    {
        ModuleInitializer.Initialize();
        ModuleServices.EnsureInitialized();

        ModuleInitializer.Cleanup();

        Assert.True(GetDisposed());
    }

    [Fact]
    public void Cleanup_CanBeCalledWhenServicesWereNeverInitialized()
    {
        var exception = Record.Exception(ModuleInitializer.Cleanup);

        Assert.Null(exception);
    }

    private static bool GetDisposed()
    {
        return (bool)(typeof(ModuleServices)
            .GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) ?? false);
    }
}
