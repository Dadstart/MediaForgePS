using System;
using System.IO;
using System.Text;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Same-directory temporary file helpers for crash-safe writes and promotions.
/// </summary>
public static class AtomicFileHelper
{
    private const string TempSuffix = ".mediaforge.tmp";

    /// <summary>
    /// Creates a sibling temporary path for <paramref name="finalPath"/> in the same directory.
    /// Preserves the original extension so tools like FFmpeg can detect the output format.
    /// </summary>
    public static string CreateTempSiblingPath(string finalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);

        var fullPath = Path.GetFullPath(finalPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
            directory = Directory.GetCurrentDirectory();

        Directory.CreateDirectory(directory);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
        var extension = Path.GetExtension(fullPath);
        return Path.Combine(
            directory,
            $"{fileNameWithoutExtension}{TempSuffix}.{Guid.NewGuid():N}{extension}");
    }

    /// <summary>
    /// Writes text to a temporary sibling file, then replaces <paramref name="finalPath"/>.
    /// </summary>
    public static void WriteTextAtomically(string finalPath, string contents, Encoding encoding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(encoding);

        var tempPath = CreateTempSiblingPath(finalPath);
        try
        {
            File.WriteAllText(tempPath, contents, encoding);
            PromoteTempFile(tempPath, finalPath);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    /// <summary>
    /// Moves <paramref name="tempPath"/> onto <paramref name="finalPath"/>, replacing an existing file when present.
    /// </summary>
    public static void PromoteTempFile(string tempPath, string finalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(finalPath));
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        File.Move(tempPath, finalPath, overwrite: true);
    }

    /// <summary>
    /// Deletes a file when it exists. Swallows expected I/O failures so callers can clean up best-effort.
    /// </summary>
    public static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException)
        {
        }
    }

    /// <summary>
    /// True when <paramref name="outputPath"/> is a platform null sink used for FFmpeg analysis passes.
    /// </summary>
    public static bool IsNullMuxerOutput(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (string.Equals(outputPath, "NUL", StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(outputPath, "/dev/null", StringComparison.Ordinal);
    }

    /// <summary>
    /// Platform null device path for FFmpeg null-muxer output.
    /// </summary>
    public static string PlatformNullDevice => OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
}
