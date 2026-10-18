using System.Runtime.InteropServices;

namespace Dadstart.Labs.MediaForge.Services.Ocr;

/// <summary>
/// Loads libSkiaSharp from the module directory before any SkiaSharp type is used.
/// PowerShell does not probe a dependency's NuGet runtime assets, and a failed
/// SkiaSharp static constructor crashes the process from a finalizer.
/// </summary>
internal static class SkiaNativeLibrary
{
    private static int _loaded;

    internal static void EnsureLoaded()
    {
        if (Volatile.Read(ref _loaded) != 0)
            return;

        var assemblyDirectory = Path.GetDirectoryName(typeof(SkiaNativeLibrary).Assembly.Location);
        if (string.IsNullOrEmpty(assemblyDirectory))
            throw new DllNotFoundException("Unable to locate the MediaForgePS assembly directory to load libSkiaSharp.");

        EnsureLoadedFrom(assemblyDirectory);
        Volatile.Write(ref _loaded, 1);
    }

    internal static void EnsureLoadedFrom(string assemblyDirectory)
    {
        var path = ResolveLibraryPath(assemblyDirectory, GetRuntimeIdentifier(), GetLibraryFileName())
            ?? throw new DllNotFoundException(BuildMissingLibraryMessage(assemblyDirectory));

        try
        {
            NativeLibrary.Load(path);
        }
        catch (DllNotFoundException ex)
        {
            throw new DllNotFoundException(
                $"Found libSkiaSharp at '{path}', but the native library or one of its dependencies could not be loaded.",
                ex);
        }
    }

    internal static string? ResolveLibraryPath(string assemblyDirectory, string runtimeIdentifier, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var ridPath = Path.Combine(assemblyDirectory, runtimeIdentifier, fileName);
        if (File.Exists(ridPath))
            return ridPath;

        var runtimesPath = Path.Combine(assemblyDirectory, "runtimes", runtimeIdentifier, "native", fileName);
        if (File.Exists(runtimesPath))
            return runtimesPath;

        var alongside = Path.Combine(assemblyDirectory, fileName);
        if (File.Exists(alongside))
            return alongside;

        return null;
    }

    private static string GetRuntimeIdentifier()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => throw new PlatformNotSupportedException($"Unsupported process architecture '{RuntimeInformation.OSArchitecture}' for libSkiaSharp.")
        };

        if (OperatingSystem.IsWindows())
            return "win-" + arch;

        if (OperatingSystem.IsLinux())
            return "linux-" + arch;

        if (OperatingSystem.IsMacOS())
            return "osx-" + arch;

        throw new PlatformNotSupportedException($"Unsupported operating system for libSkiaSharp: {RuntimeInformation.OSDescription}");
    }

    private static string GetLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
            return "libSkiaSharp.dll";

        if (OperatingSystem.IsMacOS())
            return "libSkiaSharp.dylib";

        return "libSkiaSharp.so";
    }

    private static string BuildMissingLibraryMessage(string assemblyDirectory)
    {
        var runtimeIdentifier = GetRuntimeIdentifier();
        var fileName = GetLibraryFileName();
        return $"Unable to load '{fileName}'. Expected it under '{assemblyDirectory}' in '{runtimeIdentifier}\\', 'runtimes\\{runtimeIdentifier}\\native\\', or next to MediaForgePS.dll. Rebuild the module so the SkiaSharp native asset is copied with the PowerShell module.";
    }
}
