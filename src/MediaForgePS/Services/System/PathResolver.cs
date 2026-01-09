using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Module;
using Microsoft.Extensions.Logging;

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

            if (!TryResolveProviderPath(cmdlet, path, out var providerResolvedPath))
            {
                _logger.LogWarning("Input path resolution returned no results for: {InputPath}", path);
                return false;
            }

            resolvedPath = providerResolvedPath!;
            _logger.LogDebug("Resolved input path: {ResolvedInputPath}", resolvedPath);

            // If the resolved path is the same as the input path and the file doesn't exist,
            // it means the path couldn't be resolved (file not found)
            if (resolvedPath.Equals(path, StringComparison.OrdinalIgnoreCase) && !File.Exists(resolvedPath))
            {
                _logger.LogWarning("Input path could not be resolved and file does not exist: {InputPath}", path);
                return false;
            }

            // Final validation that the file exists
            if (!File.Exists(resolvedPath))
            {
                _logger.LogWarning("Input file does not exist: {ResolvedInputPath}", resolvedPath);
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

            if (TryResolveProviderPath(cmdlet, path, out var providerResolvedPath))
                resolvedPath = providerResolvedPath!;
            else
            {
                // If path resolution fails, check if it's a relative path and resolve it
                // relative to the current working directory
                if (!Path.IsPathRooted(path))
                {
                    var currentLocation = cmdlet.SessionState.Path.CurrentLocation.Path;
                    resolvedPath = Path.GetFullPath(Path.Combine(currentLocation, path));
                    _logger.LogDebug("Resolved relative output path using current location: {ResolvedOutputPath}", resolvedPath);
                }
                else
                    // If path resolution fails and it's already absolute, try to use the path as-is
                    resolvedPath = path;
            }

            _logger.LogDebug("Resolved output path: {ResolvedOutputPath}", resolvedPath);

            // Ensure the output directory exists
            var outputDirectory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                _logger.LogInformation("Creating output directory: {OutputDirectory}", outputDirectory);
                Directory.CreateDirectory(outputDirectory);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve output path: {OutputPath}", path);
            return false;
        }
    }

    /// <summary>
    /// Attempts to resolve a PowerShell path using the provider path resolution.
    /// </summary>
    /// <param name="cmdlet">The PowerShell cmdlet to use for path resolution.</param>
    /// <param name="path">The path to resolve.</param>
    /// <param name="resolvedPath">The resolved path, or null if resolution failed.</param>
    /// <returns>True if the path was successfully resolved, false otherwise.</returns>
    public static bool TryResolveProviderPath(PSCmdlet cmdlet, string path, out string? resolvedPath)
    {
        resolvedPath = null;
        try
        {
            var providerPaths = cmdlet.GetResolvedProviderPathFromPSPath(path, out _);
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
}

