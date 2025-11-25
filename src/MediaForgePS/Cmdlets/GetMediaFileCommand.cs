using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Parsers;

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

    private IMediaModelParser _parser = new MediaModelParser();

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

        var raw = GetMediaFileRaw(resolvedPath);
        var mediaFile = _parser!.ParseFile(resolvedPath, raw);
        WriteObject(mediaFile);
    }

    private string GetMediaFileRaw(string path)
    {
        // TODO: Implement logic to fetch the raw string that can be parsed by MediaModelParser
        // This will be implemented by the user
        throw new NotImplementedException("GetMediaFileRaw must be implemented to fetch the raw media file data");
    }
}

