using Microsoft.Extensions.DependencyInjection;

namespace Dadstart.Labs.MediaForge.DependencyInjection;

public static class ServiceProviderAccessor
{
    private static IServiceProvider? _serviceProvider;

    public static IServiceProvider ServiceProvider
    {
        get
        {
            if (_serviceProvider is null)
                throw new InvalidOperationException("Service provider has not been initialized. Ensure the module is properly loaded.");

            return _serviceProvider;
        }
    }

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public static void Reset()
    {
        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();

        _serviceProvider = null;
    }
}

