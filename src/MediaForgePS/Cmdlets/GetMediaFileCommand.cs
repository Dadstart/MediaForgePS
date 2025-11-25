using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

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

    private static readonly IPlatformService _platformService = new PlatformService();
    private static readonly IFfprobeService _ffprobeService = new FfprobeService(new ExecutableService(_platformService));
    private static readonly IMediaModelParser _mediaModelParser = new MediaModelParser();
    private static readonly IMediaReaderService _mediaReaderService = new MediaReaderService(_ffprobeService, mediaModelParser: _mediaModelParser);

    protected override async void ProcessRecord()
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

        var mediaFile = await _mediaReaderService.GetMediaFile(resolvedPath);
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
}

