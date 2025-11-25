using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Dadstart.Labs.MediaForge.Logging;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

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
        services.AddSingleton<ILoggerProvider>(sp =>
        {
            var contextAccessor = sp.GetRequiredService<IPowerShellCommandContextAccessor>();
            return new PowerShellLoggerProvider(contextAccessor);
        });
        services.AddSingleton<ILoggerFactory>(sp =>
        {
            var providers = sp.GetServices<ILoggerProvider>().ToList();
            return LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                foreach (var provider in providers)
                {
                    builder.AddProvider(provider);
                }
                builder.SetMinimumLevel(LogLevel.Trace);
            });
        });
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        return services;
    }
}

