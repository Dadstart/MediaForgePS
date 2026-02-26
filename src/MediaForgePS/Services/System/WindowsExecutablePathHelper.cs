using System;
using System.IO;

namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Resolves paths to Windows executables used by subtitle cmdlets (Subtitle Edit, mkvextract).
/// </summary>
public static class WindowsExecutablePathHelper
{
    private const string SubtitleEditExeName = "SubtitleEdit.exe";
    private const string SubtitleEditFolderName = "Subtitle Edit";

    /// <summary>
    /// Full path to SubtitleEdit.exe if installed under %ProgramFiles%\Subtitle Edit; otherwise null.
    /// </summary>
    public static string? GetSubtitleEditPath()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        var path = GetSubtitleEditExpectedPath();
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Expected installation path for Subtitle Edit (used in error messages).
    /// </summary>
    public static string GetSubtitleEditExpectedPath()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, SubtitleEditFolderName, SubtitleEditExeName);
    }

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
