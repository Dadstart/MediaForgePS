using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Dadstart.Labs.MediaForge.Logging;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using System.Diagnostics;

namespace Dadstart.Labs.MediaForge.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediaForgeServices(this IServiceCollection services)
    {
        services.AddMediaForgeLogging();

        services.AddSingleton<IPlatformService, PlatformService>();
        services.AddSingleton<IExecutableService, ExecutableService>();
        services.AddSingleton<IFfprobeService, FfprobeService>();
        services.AddSingleton<IFfmpegService, FfmpegService>();
        services.AddSingleton<IMediaModelParser, MediaModelParser>();
        services.AddSingleton<IMediaReaderService, MediaReaderService>();

        return services;
    }

    public static IServiceCollection AddMediaForgeLogging(this IServiceCollection services)
    {
        services.AddSingleton<IPowerShellCommandContextAccessor, PowerShellCommandContext>();
        
        // Add logging infrastructure first - this enables generic logger resolution (ILogger<T>)
        // This must be called before registering custom providers to ensure proper setup
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        
        // Register the PowerShell logger provider
        // The default factory from AddLogging will automatically pick up all registered ILoggerProvider instances
        services.AddSingleton<ILoggerProvider>(sp =>
        {
            var contextAccessor = sp.GetRequiredService<IPowerShellCommandContextAccessor>();
            return new PowerShellLoggerProvider(contextAccessor);
        });
        
        // Override the factory to ensure only our PowerShell logger provider is used
        // This factory will be used for creating loggers and supports generic logger resolution
        services.AddSingleton<ILoggerFactory>(sp =>
        {
            var provider = sp.GetRequiredService<ILoggerProvider>();
            return LoggerFactory.Create(factoryBuilder =>
            {
                factoryBuilder.ClearProviders();
                factoryBuilder.AddProvider(provider);
                factoryBuilder.SetMinimumLevel(LogLevel.Trace);
            });
        });
        
        services.AddSingleton<IDebuggerService, DebuggerService>();

        return services;
    }
}


