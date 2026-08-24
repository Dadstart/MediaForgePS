using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Services.TvDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Module-level service container. Builds a single IServiceProvider for the module,
/// registers logging and other services, and exposes helper methods to resolve services.
/// Call EnsureInitialized() from cmdlets before resolving services.
/// Call Dispose() on module unload if you want to release disposable singletons.
/// Dispose is deferred until all in-flight cmdlets finish when Remove-Module runs during active work.
/// </summary>
public static class ModuleServices
{
    private static readonly object _sync = new();
    private static IServiceProvider? _provider;
    private static bool _initialized;
    private static bool _disposed;
    private static bool _disposeRequested;
    private static int _inFlightCmdletCount;

    /// <summary>
    /// Marks a cmdlet as using module services until <see cref="ExitCmdlet"/> is called.
    /// </summary>
    internal static void EnterCmdlet()
    {
        lock (_sync)
        {
            EnsureInitializedUnlocked();
            _inFlightCmdletCount++;
        }
    }

    /// <summary>
    /// Releases a cmdlet scope. Completes a pending <see cref="Dispose"/> when the last cmdlet exits.
    /// </summary>
    internal static void ExitCmdlet()
    {
        lock (_sync)
        {
            if (_inFlightCmdletCount > 0)
                _inFlightCmdletCount--;

            if (_disposeRequested && _inFlightCmdletCount == 0 && !_disposed)
                DisposeCoreUnlocked();
        }
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_sync)
            EnsureInitializedUnlocked();
    }

    private static void EnsureInitializedUnlocked()
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
        services.AddSingleton<ITvDbCredentialProvider, EnvironmentTvDbCredentialProvider>();
        services.AddSingleton<ITvDbClient, TvDbClient>();
        services.AddSingleton<ISeriesProcessingService, SeriesProcessingService>();
        services.AddSingleton<IBonusProcessingService, BonusProcessingService>();
        if (OperatingSystem.IsWindows())
            services.AddSingleton<IImageSubtitleOcrConverter, LibseImageSubtitleOcrConverter>();
        else
            services.AddSingleton<IImageSubtitleOcrConverter, UnavailableImageSubtitleOcrConverter>();

        _provider = services.BuildServiceProvider(validateScopes: true);
        _initialized = true;
        _disposed = false;
        _disposeRequested = false;
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
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposeRequested = true;
            if (_inFlightCmdletCount == 0)
                DisposeCoreUnlocked();
        }
    }

    /// <summary>
    /// Resets module services state for test isolation.
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (_sync)
        {
            if (_provider is IDisposable d)
            {
                try { d.Dispose(); }
                catch { }
            }

            _provider = null;
            _initialized = false;
            _disposed = false;
            _disposeRequested = false;
            _inFlightCmdletCount = 0;
        }
    }

    private static void DisposeCoreUnlocked()
    {
        if (_disposed)
            return;

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
