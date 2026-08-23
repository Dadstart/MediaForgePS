namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Service for resolving and validating file paths in PowerShell contexts.
/// </summary>
public interface IPathResolver
{
    /// <summary>
    /// Resolves a PowerShell path and validates that the file exists.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <param name="resolvedPath">The resolved path if successful.</param>
    /// <returns>True if the path was resolved and the file exists, false otherwise.</returns>
    bool TryResolveInputPath(string path, out string resolvedPath);

    /// <summary>
    /// Resolves a PowerShell path for output without creating directories.
    /// Call <see cref="EnsureOutputDirectoryExists"/> after ShouldProcess (or equivalent) succeeds.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <param name="resolvedPath">The resolved path if successful.</param>
    /// <returns>True if the path was resolved successfully, false otherwise.</returns>
    bool TryResolveOutputPath(string path, out string resolvedPath);

    /// <summary>
    /// Creates the parent directory of <paramref name="resolvedFilePath"/> when it does not already exist.
    /// Call only after ShouldProcess / confirmation succeeds so -WhatIf stays side-effect free.
    /// </summary>
    /// <param name="resolvedFilePath">Resolved output file path whose parent directory should exist.</param>
    void EnsureOutputDirectoryExists(string resolvedFilePath);
}

