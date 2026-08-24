using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Module;
using Microsoft.Extensions.Logging;
using PathSafetyHelper = Dadstart.Labs.MediaForge.Services.PathSafetyHelper;

namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Service for resolving and validating file paths in PowerShell contexts.
/// </summary>
public class PathResolver : IPathResolver
{
    private readonly ILogger<PathResolver> _logger;

    public PathResolver(ILogger<PathResolver> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool TryResolveInputPath(string path, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        try
        {
            _logger.LogDebug("Resolving PowerShell input path: {InputPath}", path);

            var cmdlet = CmdletContext.Current;
            if (cmdlet == null)
            {
                _logger.LogError("No cmdlet context available for path resolution");
                return false;
            }

            var paths = new PsCmdletIO(cmdlet).Paths;
            if (!TryResolveProviderPath(paths, path, out var providerResolvedPath))
            {
                _logger.LogDebug("Input path resolution returned no results for: {InputPath}", path);
                return false;
            }

            resolvedPath = providerResolvedPath!;
            _logger.LogDebug("Resolved input path: {ResolvedInputPath}", resolvedPath);

            // If the resolved path is the same as the input path and the file doesn't exist,
            // it means the path couldn't be resolved (file not found)
            if (resolvedPath.Equals(path, StringComparison.OrdinalIgnoreCase) && !File.Exists(resolvedPath))
            {
                _logger.LogDebug("Input path could not be resolved and file does not exist: {InputPath}", path);
                return false;
            }

            // Final validation that the file exists
            if (!File.Exists(resolvedPath))
            {
                _logger.LogDebug("Input file does not exist: {ResolvedInputPath}", resolvedPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve input path: {InputPath}", path);
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryResolveOutputPath(string path, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        try
        {
            _logger.LogDebug("Resolving PowerShell output path: {OutputPath}", path);

            var cmdlet = CmdletContext.Current;
            if (cmdlet == null)
            {
                _logger.LogError("No cmdlet context available for path resolution");
                return false;
            }

            var paths = new PsCmdletIO(cmdlet).Paths;
            if (TryResolveProviderPath(paths, path, out var providerResolvedPath))
                resolvedPath = providerResolvedPath!;
            else
            {
                // If path resolution fails, check if it's a relative path and resolve it
                // relative to the current working directory
                if (!Path.IsPathRooted(path))
                {
                    var currentLocation = paths.CurrentLocationPath;
                    resolvedPath = Path.GetFullPath(Path.Combine(currentLocation, path));
                    _logger.LogDebug("Resolved relative output path using current location: {ResolvedOutputPath}", resolvedPath);
                }
                else
                    // If path resolution fails and it's already absolute, try to use the path as-is
                    resolvedPath = path;
            }

            _logger.LogDebug("Resolved output path: {ResolvedOutputPath}", resolvedPath);

            // Do not create directories here: callers must call EnsureOutputDirectoryExists after
            // ShouldProcess succeeds so -WhatIf does not create filesystem side effects.
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve output path: {OutputPath}", path);
            return false;
        }
    }

    /// <inheritdoc />
    public void EnsureOutputDirectoryExists(string resolvedFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedFilePath);

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(resolvedFilePath));
        if (string.IsNullOrEmpty(outputDirectory) || Directory.Exists(outputDirectory))
            return;

        _logger.LogInformation("Creating output directory: {OutputDirectory}", outputDirectory);
        Directory.CreateDirectory(outputDirectory);
    }

    /// <summary>
    /// Attempts to resolve a PowerShell path using the provider path resolution.
    /// Requires the path to exist.
    /// </summary>
    /// <param name="paths">Path context for provider resolution.</param>
    /// <param name="path">The path to resolve.</param>
    /// <param name="resolvedPath">The resolved path, or null if resolution failed.</param>
    /// <returns>True if the path was successfully resolved, false otherwise.</returns>
    public static bool TryResolveProviderPath(ICmdletPathContext paths, string path, out string? resolvedPath)
    {
        resolvedPath = null;
        try
        {
            var escapedPath = EscapeLiteralProviderPath(path);
            var providerPaths = paths.GetResolvedProviderPaths(escapedPath);
            if (providerPaths.Count > 0)
            {
                resolvedPath = providerPaths[0];
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves a PowerShell path using the session's current location without requiring the path to exist.
    /// Use for directory paths that may not yet exist (e.g. output directory).
    /// </summary>
    /// <param name="paths">Path context for provider resolution.</param>
    /// <param name="path">The path to resolve.</param>
    /// <param name="resolvedPath">The resolved path, or null if resolution failed.</param>
    /// <returns>True if the path was successfully resolved, false otherwise.</returns>
    public static bool TryGetUnresolvedProviderPath(ICmdletPathContext paths, string path, out string? resolvedPath)
    {
        resolvedPath = null;
        try
        {
            var escapedPath = EscapeLiteralProviderPath(path);
            resolvedPath = paths.GetUnresolvedProviderPath(escapedPath);
            return !string.IsNullOrEmpty(resolvedPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves each path to a file or directory that exists. Used by subtitle cmdlets that accept file/directory paths.
    /// </summary>
    public static List<(string ResolvedPath, bool IsDirectory)> ResolveFileOrDirectoryPaths(
        ICmdletPathContext paths,
        IEnumerable<string> inputPaths,
        ILogger? logger,
        Action<ErrorRecord> writeError)
    {
        var result = new List<(string, bool)>();
        foreach (var path in inputPaths)
        {
            if (TryResolveProviderPath(paths, path, out var resolved))
            {
                if (Directory.Exists(resolved))
                    result.Add((resolved!, true));
                else if (File.Exists(resolved))
                    result.Add((resolved!, false));
                else
                    logger?.LogDebug("Resolved path does not exist: {Path}", resolved);
            }
            else if (TryGetUnresolvedProviderPath(paths, path, out var unresolved))
            {
                if (Directory.Exists(unresolved))
                    result.Add((unresolved!, true));
                else if (File.Exists(unresolved))
                    result.Add((unresolved!, false));
                else
                    writeError(new ErrorRecord(new FileNotFoundException("File or directory not found.", path), "PathNotFound", ErrorCategory.ObjectNotFound, path));
            }
            else
                writeError(new ErrorRecord(new FileNotFoundException("File or directory not found.", path), "PathNotFound", ErrorCategory.ObjectNotFound, path));
        }
        return result;
    }

    /// <summary>
    /// Resolves an output path; on failure writes an error via writeError and returns null.
    /// </summary>
    public static string? ResolveOutputPathOrNull(IPathResolver pathResolver, string path, Action<ErrorRecord> writeError)
    {
        if (pathResolver.TryResolveOutputPath(path, out var resolved))
            return resolved;
        writeError(new ErrorRecord(new InvalidOperationException($"Failed to resolve output path: {path}"), "OutputPathResolutionFailed", ErrorCategory.InvalidArgument, path));
        return null;
    }

    /// <summary>
    /// Resolves a backup directory path; creates the directory. On failure writes an error and returns false.
    /// </summary>
    public static bool ResolveBackupPath(IPathResolver pathResolver, string? backupPath, Action<ErrorRecord> writeError, out string? resolvedBackupRoot)
    {
        resolvedBackupRoot = null;
        if (string.IsNullOrWhiteSpace(backupPath))
            return true;
        if (!pathResolver.TryResolveOutputPath(backupPath, out var resolved))
        {
            writeError(new ErrorRecord(new InvalidOperationException($"Failed to resolve backup path: {backupPath}"), "BackupPathResolutionFailed", ErrorCategory.InvalidArgument, backupPath));
            return false;
        }
        resolvedBackupRoot = resolved;
        Directory.CreateDirectory(resolvedBackupRoot);
        return true;
    }

    /// <summary>
    /// Copies a file to a backup location under backupRoot using the given relative path. Creates parent directories as needed.
    /// </summary>
    public static void CopyFileToBackup(string backupRoot, string sourceFilePath, string relativePath)
    {
        var backupDest = PathSafetyHelper.GetContainedRelativePath(backupRoot, relativePath);
        var backupDir = Path.GetDirectoryName(backupDest);
        if (!string.IsNullOrEmpty(backupDir))
            Directory.CreateDirectory(backupDir);
        File.Copy(sourceFilePath, backupDest, overwrite: true);
    }

    /// <summary>
    /// Escapes a path for literal PowerShell provider resolution.
    /// Collapses any existing PowerShell wildcard escapes first so escaping is idempotent
    /// (both <c>file [DVD].mkv</c> and <c>file `[DVD`].mkv</c> resolve to the same literal path).
    /// </summary>
    /// <param name="path">Path that may contain unescaped or already-escaped wildcard characters.</param>
    /// <returns>Path escaped for literal provider resolution.</returns>
    public static string EscapeLiteralProviderPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return WildcardPattern.Escape(CollapseWildcardEscapes(path));
    }

    /// <summary>
    /// Removes PowerShell wildcard escape backticks so <see cref="WildcardPattern.Escape"/> is not applied twice.
    /// </summary>
    internal static string CollapseWildcardEscapes(string path)
    {
        var normalized = path;
        string previous;
        do
        {
            previous = normalized;
            normalized = previous
                .Replace("`[", "[", StringComparison.Ordinal)
                .Replace("`]", "]", StringComparison.Ordinal)
                .Replace("`*", "*", StringComparison.Ordinal)
                .Replace("`?", "?", StringComparison.Ordinal);
        } while (!string.Equals(normalized, previous, StringComparison.Ordinal));

        return normalized;
    }
}

