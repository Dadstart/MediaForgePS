using System.Reflection;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public sealed class ModuleServicesTests : IDisposable
{
    public ModuleServicesTests()
    {
        ModuleServices.ResetForTesting();
    }

    public void Dispose()
    {
        ModuleServices.ResetForTesting();
    }

    [Fact]
    public void Dispose_WhenNoCmdletsInFlight_DisposesProviderImmediately()
    {
        ModuleServices.EnsureInitialized();

        ModuleServices.Dispose();

        Assert.Null(GetProvider());
        Assert.True(GetDisposed());
    }

    [Fact]
    public void Dispose_WhenCmdletInFlight_DefersUntilExitCmdlet()
    {
        ModuleServices.EnsureInitialized();
        var providerBeforeDispose = GetProvider();
        Assert.NotNull(providerBeforeDispose);

        ModuleServices.EnterCmdlet();
        try
        {
            ModuleServices.Dispose();

            Assert.Same(providerBeforeDispose, GetProvider());
            Assert.False(GetDisposed());
            Assert.True(GetDisposeRequested());
        }
        finally
        {
            ModuleServices.ExitCmdlet();
        }

        Assert.Null(GetProvider());
        Assert.True(GetDisposed());
    }

    [Fact]
    public void Dispose_WhenMultipleCmdletsInFlight_DefersUntilLastExitCmdlet()
    {
        ModuleServices.EnsureInitialized();
        var providerBeforeDispose = GetProvider();

        ModuleServices.EnterCmdlet();
        ModuleServices.EnterCmdlet();
        try
        {
            ModuleServices.Dispose();

            Assert.Same(providerBeforeDispose, GetProvider());
            Assert.True(GetDisposeRequested());

            ModuleServices.ExitCmdlet();
            Assert.Same(providerBeforeDispose, GetProvider());
            Assert.False(GetDisposed());
        }
        finally
        {
            ModuleServices.ExitCmdlet();
        }

        Assert.Null(GetProvider());
        Assert.True(GetDisposed());
    }

    [Fact]
    public void EnsureInitialized_AfterDeferredDispose_CompletesReinitialization()
    {
        ModuleServices.EnsureInitialized();
        ModuleServices.EnterCmdlet();
        ModuleServices.Dispose();
        ModuleServices.ExitCmdlet();

        ModuleServices.EnsureInitialized();

        Assert.NotNull(GetProvider());
        Assert.False(GetDisposed());
        Assert.False(GetDisposeRequested());
    }

    [Fact]
    public void GetRequiredService_WhenDisposeDeferredWhileCmdletInFlight_StillResolvesServices()
    {
        ModuleServices.EnsureInitialized();
        ModuleServices.EnterCmdlet();
        try
        {
            ModuleServices.Dispose();

            var platformService = ModuleServices.GetRequiredService<IPlatformService>();

            Assert.NotNull(platformService);
        }
        finally
        {
            ModuleServices.ExitCmdlet();
        }
    }

    private static IServiceProvider? GetProvider()
    {
        return typeof(ModuleServices)
            .GetField("_provider", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) as IServiceProvider;
    }

    private static bool GetDisposed()
    {
        return (bool)(typeof(ModuleServices)
            .GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) ?? false);
    }

    private static bool GetDisposeRequested()
    {
        return (bool)(typeof(ModuleServices)
            .GetField("_disposeRequested", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) ?? false);
    }
}
