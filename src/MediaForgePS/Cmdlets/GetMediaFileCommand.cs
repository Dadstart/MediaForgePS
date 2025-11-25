using System;
using System.IO;
using System.Management.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Dadstart.Labs.MediaForge.DependencyInjection;
using Dadstart.Labs.MediaForge.Logging;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;

namespace Dadstart.Labs.MediaForge.Cmdlets;

[Cmdlet(VerbsCommon.Get, "MediaFile")]
[OutputType(typeof(MediaFile))]
public class GetMediaFileCommand : PSCmdlet
{
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to the media file")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    private IMediaReaderService? _mediaReaderService;
    private ILogger<GetMediaFileCommand>? _logger;
    private IPowerShellCommandContextAccessor? _contextAccessor;

    private IMediaReaderService MediaReaderService
    {
        get
        {
            return _mediaReaderService ??= ServiceProviderAccessor.ServiceProvider.GetRequiredService<IMediaReaderService>();
        }
    }

    private ILogger<GetMediaFileCommand> Logger
    {
        get
        {
            return _logger ??= ServiceProviderAccessor.ServiceProvider.GetRequiredService<ILogger<GetMediaFileCommand>>();
        }
    }

    private IPowerShellCommandContextAccessor ContextAccessor
    {
        get
        {
            return _contextAccessor ??= ServiceProviderAccessor.ServiceProvider.GetRequiredService<IPowerShellCommandContextAccessor>();
        }
    }

    protected override void BeginProcessing()
    {
        ContextAccessor.SetCurrentContext(this);
        Logger.LogDebug("Begin processing Get-MediaFile command for path: {Path}", Path);
    }

    protected override void EndProcessing()
    {
        Logger.LogDebug("End processing Get-MediaFile command");
        ContextAccessor.SetCurrentContext(null);
    }

    protected override void ProcessRecord()
    {
        Logger.LogInformation("Processing Get-MediaFile request for path: {Path}", Path);

        string resolvedPath;
        try
        {
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

            Logger.LogDebug("Reading media file information: {ResolvedPath}", resolvedPath);
            var task = MediaReaderService.GetMediaFile(resolvedPath).ConfigureAwait(false).GetAwaiter();
            var mediaFile = task.GetResult();
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

