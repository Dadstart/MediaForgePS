using System;
using System.IO;

namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Resolves paths to Windows executables used by subtitle cmdlets (mkvextract)
/// and provides shared platform-guard messaging for Windows-only process launches.
/// </summary>
public static class WindowsExecutablePathHelper
{
    /// <summary>
    /// Warning/error text when Matroska VobSub extraction via mkvextract is attempted off Windows.
    /// </summary>
    public const string MkvextractUnsupportedPlatformMessage =
        "mkvextract is only available on Windows. Matroska VobSub (dvd_subtitle) extraction is not supported on this platform.";

    /// <summary>
    /// Whether <paramref name="command"/> looks like a Windows PE executable (ends with <c>.exe</c>).
    /// </summary>
    public static bool IsWindowsExecutableCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        return command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// User-facing message when a Windows <c>.exe</c> would be launched on an unsupported platform.
    /// </summary>
    public static string FormatWindowsExecutableUnsupportedMessage(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var displayName = Path.GetFileName(command);
        if (string.IsNullOrEmpty(displayName))
            displayName = command;

        return $"Windows executable '{displayName}' cannot run on this platform. This operation is supported on Windows only.";
    }

    /// <summary>
    /// Full path to mkvextract.exe if mkvtoolnix is installed under %ProgramFiles%; otherwise null.
    /// Always returns null on non-Windows platforms.
    /// </summary>
    public static string? GetMkvextractPath()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var path = Path.Combine(programFiles, "mkvtoolnix", "mkvextract.exe");
        return File.Exists(path) ? path : null;
    }
}
