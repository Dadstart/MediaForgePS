using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dadstart.Labs.MediaForgePS.Services;

/// <summary>
/// Factory for creating and managing the dependency injection service provider.
/// </summary>
public static class ServiceProviderFactory
{
    private static IServiceProvider? _serviceProvider;
    private static readonly object _lock = new();

    /// <summary>
    /// Current service provider instance.
    /// </summary>
    public static IServiceProvider Current
    {
        get
        {
            if (_serviceProvider == null)
            {
                lock (_lock)
                {
                    _serviceProvider ??= CreateServiceProvider();
                }
            }
            return _serviceProvider;
        }
    }

    /// <summary>
    /// Creates and configures the service provider with all registered services.
    /// </summary>
    /// <returns>Configured service provider.</returns>
    public static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Register HttpClient as transient (new instance per request)
        services.AddTransient<HttpClient>(sp =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            return client;
        });

        // Register services
        services.AddTransient<HolidayScraper>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Sets the service provider instance (primarily for testing).
    /// </summary>
    /// <param name="serviceProvider">Service provider to use.</param>
    internal static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        lock (_lock)
        {
            _serviceProvider = serviceProvider;
        }
    }

    /// <summary>
    /// Resets the service provider to null (primarily for testing).
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _serviceProvider = null;
        }
    }

    public static void SetServiceProvider(ServiceProvider serviceProvider)
    {
        throw new NotImplementedException();
    }
}
