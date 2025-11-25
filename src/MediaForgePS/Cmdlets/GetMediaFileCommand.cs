using System;
using System.IO;
using System.Management.Automation;
using Microsoft.Extensions.DependencyInjection;
using Dadstart.Labs.MediaForge.DependencyInjection;
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

    private IMediaReaderService MediaReaderService
    {
        get
        {
            return _mediaReaderService ??= ServiceProviderAccessor.ServiceProvider.GetRequiredService<IMediaReaderService>();
        }
    }

    protected override void ProcessRecord()
    {
        string resolvedPath;
        try
        {
            var providerPaths = GetResolvedProviderPathFromPSPath(Path, out var provider);
            if (providerPaths.Count == 0)
            {
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"Media file not found: {Path}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    Path);
                WriteError(errorRecord);
                return;
            }
            resolvedPath = providerPaths[0];
        }
        catch (Exception ex)
        {
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
            if (!File.Exists(resolvedPath))
            {
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"Media file not found: {resolvedPath}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    resolvedPath);
                WriteError(errorRecord);
                return;
            }

            var task = MediaReaderService.GetMediaFile(resolvedPath).ConfigureAwait(false).GetAwaiter();
            var mediaFile = task.GetResult();
            if (mediaFile is null)
            {
                var errorRecord = new ErrorRecord(
                    new Exception($"Failed to get media file information: {resolvedPath}"),
                    "MediaFileReadFailed",
                    ErrorCategory.ReadError,
                    resolvedPath);
                WriteError(errorRecord);
                return;
            }

            WriteObject(mediaFile);

        }
        catch (Exception ex)
        {
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

