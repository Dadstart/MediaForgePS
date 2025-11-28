using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Retrieves detailed information about a media file, including format, streams, and chapters.
/// </summary>
/// <remarks>
/// This cmdlet uses ffprobe to analyze media files and returns a <see cref="MediaFile"/> object
/// containing comprehensive metadata about the file's structure and content.
/// </remarks>
[Cmdlet(VerbsCommon.Get, "MediaFile")]
[OutputType(typeof(MediaFile))]
public class GetMediaFileCommand : CmdletBase
{
    /// <summary>
    /// Path to the media file to analyze. Can be a relative or absolute path, and supports
    /// PowerShell path resolution including wildcards and provider paths.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to the media file")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    private IMediaReaderService? _mediaReaderService;

    /// <summary>
    /// Media reader service instance for retrieving media file information.
    /// </summary>
    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    /// <summary>
    /// Processes the media file path, resolves it, validates existence, and retrieves media information.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Get-MediaFile request for path: {Path}", Path);

        string resolvedPath;
        try
        {
            // Resolve PowerShell path (handles wildcards, provider paths, relative paths, etc.)
            Logger.LogDebug("Resolving PowerShell path: {Path}", Path);
            var providerPaths = GetResolvedProviderPathFromPSPath(Path, out var provider);
            if (providerPaths.Count == 0)
            {
                Logger.LogWarning("Path resolution returned no results for: {Path}", Path);
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"Media file not found: {Path}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    Path);
                WriteError(errorRecord);
                return;
            }
            resolvedPath = providerPaths[0];
            Logger.LogDebug("Resolved path: {ResolvedPath}", resolvedPath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resolve path: {Path}", Path);
            var errorRecord = new ErrorRecord(
                ex,
                "PathResolutionFailed",
                ErrorCategory.InvalidArgument,
                Path);
            WriteError(errorRecord);
            return;
        }

        try
        {
            // Verify the file exists before attempting to read it
            Logger.LogDebug("Checking if file exists: {ResolvedPath}", resolvedPath);
            if (!File.Exists(resolvedPath))
            {
                Logger.LogWarning("File does not exist: {ResolvedPath}", resolvedPath);
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"Media file not found: {resolvedPath}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    resolvedPath);
                WriteError(errorRecord);
                return;
            }

            // Read media file information using the media reader service
            // Note: Using GetAwaiter().GetResult() to synchronously wait for the async operation
            // This is acceptable in PowerShell cmdlets which must be synchronous
            Logger.LogDebug("Reading media file information: {ResolvedPath}", resolvedPath);
            var mediaFile = MediaReaderService.GetMediaFileAsync(resolvedPath).ConfigureAwait(false).GetAwaiter().GetResult();
            if (mediaFile is null)
            {
                Logger.LogWarning("Media file information is null for: {ResolvedPath}", resolvedPath);
                var errorRecord = new ErrorRecord(
                    new Exception($"Failed to get media file information: {resolvedPath}"),
                    "MediaFileReadFailed",
                    ErrorCategory.ReadError,
                    resolvedPath);
                WriteError(errorRecord);
                return;
            }

            Logger.LogInformation("Successfully retrieved media file: {ResolvedPath}", resolvedPath);
            WriteObject(mediaFile);

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while processing media file: {ResolvedPath}", resolvedPath);
            var errorRecord = new ErrorRecord(
                ex,
                "MediaFileReadFailed",
                ErrorCategory.ReadError,
                resolvedPath);
            WriteError(errorRecord);
            return;
        }
    }
}

