using Microsoft.Extensions.DependencyInjection;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediaForgeServices(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformService, PlatformService>();
        services.AddSingleton<IExecutableService, ExecutableService>();
        services.AddSingleton<IFfprobeService, FfprobeService>();
        services.AddSingleton<IFfmpegService, FfmpegService>();
        services.AddSingleton<IMediaModelParser, MediaModelParser>();
        services.AddSingleton<IMediaReaderService, MediaReaderService>();

        return services;
    }
}

