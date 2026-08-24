using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Services.TvDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.ComponentTests.Infrastructure;

/// <summary>
/// Builds a <see cref="ModuleServices"/> provider with real media services and a stubbed TVDb client.
/// </summary>
public static class ComponentServiceProviderFactory
{
    public static ServiceProvider CreateWithStubTvDb(ITvDbClient tvDbClient)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(new PowerShellLoggerProvider());
        });

        services.AddSingleton<IPlatformService, PlatformService>();
        services.AddSingleton<IDebuggerService, DebuggerService>();
        services.AddSingleton<IExecutableService, ExecutableService>();
        services.AddSingleton<IPathResolver, PathResolver>();
        services.AddSingleton<IFfprobeService, FfprobeService>();
        services.AddSingleton<IFfmpegService, FfmpegService>();
        services.AddSingleton<IMediaModelParser, MediaModelParser>();
        services.AddSingleton<IMediaReaderService, MediaReaderService>();
        services.AddSingleton<IAudioTrackMappingService, AudioTrackMappingService>();
        services.AddSingleton<IMediaConversionService, MediaConversionService>();
        services.AddSingleton<ITvDbCredentialProvider, EnvironmentTvDbCredentialProvider>();
        services.AddSingleton(tvDbClient);
        services.AddSingleton<ISeriesProcessingService, SeriesProcessingService>();
        services.AddSingleton<IBonusProcessingService, BonusProcessingService>();

        if (OperatingSystem.IsWindows())
            services.AddSingleton<IImageSubtitleOcrConverter, LibseImageSubtitleOcrConverter>();
        else
            services.AddSingleton<IImageSubtitleOcrConverter, UnavailableImageSubtitleOcrConverter>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
