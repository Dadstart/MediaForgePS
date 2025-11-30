using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
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
    private IPathResolver? _pathResolver;

    /// <summary>
    /// Media reader service instance for retrieving media file information.
    /// </summary>
    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Processes the media file path, resolves it, validates existence, and retrieves media information.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Get-MediaFile request for path: {Path}", Path);

        string resolvedPath;
        if (!PathResolver.TryResolveInputPath(Path, out resolvedPath))
        {
            var errorRecord = new ErrorRecord(
                new FileNotFoundException($"Media file not found: {Path}"),
                "FileNotFound",
                ErrorCategory.ObjectNotFound,
                Path);
            WriteError(errorRecord);
            return;
        }

        try
        {
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

