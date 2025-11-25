using Microsoft.Extensions.DependencyInjection;

namespace Dadstart.Labs.MediaForge.DependencyInjection;

public static class ModuleInitializer
{
    public static void Initialize()
    {
        var services = new ServiceCollection();
        services.AddMediaForgeServices();
        var serviceProvider = services.BuildServiceProvider();
        ServiceProviderAccessor.Initialize(serviceProvider);
    }

    public static void Cleanup()
    {
        ServiceProviderAccessor.Reset();
    }
}

