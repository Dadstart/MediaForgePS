using System;
using System.IO;

namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Resolves paths to Windows executables used by subtitle cmdlets (mkvextract).
/// </summary>
public static class WindowsExecutablePathHelper
{
    /// <summary>
    /// Full path to mkvextract.exe if mkvtoolnix is installed under %ProgramFiles%; otherwise null.
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
