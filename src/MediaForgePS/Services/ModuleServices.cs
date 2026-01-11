using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Module-level service container. Builds a single IServiceProvider for the module,
/// registers logging and other services, and exposes helper methods to resolve services.
/// Call EnsureInitialized() from cmdlets before resolving services.
/// Call Dispose() on module unload if you want to release disposable singletons.
/// </summary>
public static class ModuleServices
{
    private static readonly object _sync = new();
    private static IServiceProvider? _provider;
    private static bool _initialized;
    private static bool _disposed;

    public static void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_sync)
        {
            if (_initialized)
                return;

            var services = new ServiceCollection();

            // configure logging to use the PowerShell logger provider
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(new PowerShellLoggerProvider());
            });

            // register application services
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

            _provider = services.BuildServiceProvider(validateScopes: true);
            _initialized = true;
            _disposed = false;
        }
    }

    public static T GetRequiredService<T>() where T : notnull
    {
        EnsureInitialized();
        if (_provider == null) throw new InvalidOperationException("Service provider not initialized.");
        return _provider.GetRequiredService<T>();
    }

    public static T? GetService<T>() where T : class
    {
        EnsureInitialized();
        if (_provider == null) return null;
        return _provider.GetService<T>();
    }

    public static void Dispose()
    {
        if (_disposed) return;

        lock (_sync)
        {
            if (_disposed) return;

            if (_provider is IDisposable d)
            {
                try { d.Dispose(); }
                catch { /* Exceptions during module unload are ignored to prevent cascading failures */ }
            }

            _provider = null;
            _initialized = false;
            _disposed = true;
        }
    }
}
