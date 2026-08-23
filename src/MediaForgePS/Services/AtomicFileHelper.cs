using System;
using System.IO;
using System.Text;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Temporary file helpers for crash-safe writes, conversion staging, and promotions.
/// </summary>
public static class AtomicFileHelper
{
    private const string TempSuffix = ".mediaforge.tmp";
    private const string TempDirectoryPrefix = "MediaForgePS_";

    /// <summary>
    /// Creates a unique directory under the system temp path for staging conversion outputs.
    /// </summary>
    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), TempDirectoryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Creates a staging path under a unique system temp directory for <paramref name="finalPath"/>.
    /// Preserves the final file name (and extension) so tools like FFmpeg can detect the output format.
    /// Callers must delete the parent directory with <see cref="TryDeleteDirectory"/> when finished.
    /// </summary>
    public static string CreateTempOutputPath(string finalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);

        var fileName = Path.GetFileName(Path.GetFullPath(finalPath));
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("Final path must include a file name.", nameof(finalPath));

        return Path.Combine(CreateTempDirectory(), fileName);
    }

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
    /// <param name="finalPath">Destination path.</param>
    /// <param name="contents">Text to write.</param>
    /// <param name="encoding">Text encoding.</param>
    /// <param name="overwrite">When false, throws if <paramref name="finalPath"/> already exists.</param>
    public static void WriteTextAtomically(string finalPath, string contents, Encoding encoding, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(encoding);

        var tempPath = CreateTempSiblingPath(finalPath);
        try
        {
            File.WriteAllText(tempPath, contents, encoding);
            PromoteTempFile(tempPath, finalPath, overwrite);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    /// <summary>
    /// Moves <paramref name="tempPath"/> onto <paramref name="finalPath"/>.
    /// </summary>
    /// <param name="tempPath">Staged temporary file.</param>
    /// <param name="finalPath">Final destination path.</param>
    /// <param name="overwrite">When false, throws if <paramref name="finalPath"/> already exists.</param>
    /// <exception cref="IOException">Thrown when the destination exists and <paramref name="overwrite"/> is false.</exception>
    public static void PromoteTempFile(string tempPath, string finalPath, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(finalPath));
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        if (!overwrite && File.Exists(finalPath))
            throw new IOException($"Output file already exists: {finalPath}. Use -Force to overwrite.");

        File.Move(tempPath, finalPath, overwrite: overwrite);
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
    /// Deletes a directory when it exists. Swallows expected I/O failures so callers can clean up best-effort.
    /// </summary>
    public static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
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
