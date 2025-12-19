using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts a media file from one format to another using Ffmpeg.
/// </summary>
/// <remarks>
/// This cmdlet uses ffmpeg to convert media files between different formats, codecs, and containers.
/// </remarks>
[Cmdlet(VerbsData.Convert, "MediaFile")]
[OutputType(typeof(bool))]
public class ConvertMediaFileCommand : ConvertMediaCommandBase
{
    /// <summary>
    /// Path to the input media file to convert. Can be a relative or absolute path, and supports
    /// PowerShell path resolution including wildcards and provider paths.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to the input media file")]
    [ValidateNotNullOrEmpty]
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the output media file. Can be a relative or absolute path.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to the output media file")]
    [ValidateNotNullOrEmpty]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Creates an error record for a file not found error.
    /// </summary>
    /// <param name="path">The path that was not found.</param>
    /// <param name="message">The error message.</param>
    /// <returns>An ErrorRecord for the file not found error.</returns>
    private ErrorRecord CreateFileNotFoundErrorRecord(string path, string message)
    {
        return new ErrorRecord(
            new FileNotFoundException(message),
            "FileNotFound",
            ErrorCategory.ObjectNotFound,
            path);
    }

    private void WriteFileNotFoundErrorRecord(string path, string message)
    {
        WriteError(CreateFileNotFoundErrorRecord(path, message));
    }

    /// <summary>
    /// Processes the media file conversion request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Convert-MediaFile request: {InputPath} -> {OutputPath}", InputPath, OutputPath);

        string resolvedInputPath;
        if (!PathResolver.TryResolveInputPath(InputPath, out resolvedInputPath))
        {
            WriteFileNotFoundErrorRecord(InputPath, $"Input media file not found: {InputPath}");
            return;
        }

        string resolvedOutputPath;
        if (!PathResolver.TryResolveOutputPath(OutputPath, out resolvedOutputPath))
        {
            WritePathErrorRecord(OutputPath, $"Failed to resolve output path: {OutputPath}");
            return;
        }

        try
        {
            // Perform the conversion
            // Note: Using GetAwaiter().GetResult() to synchronously wait for the async operation
            // This is acceptable in PowerShell cmdlets which must be synchronous
            bool success = ConvertMediaFile(resolvedInputPath, resolvedOutputPath);

            if (success)
            {
                Logger.LogInformation("Successfully converted media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
                WriteObject(true);
            }
            else
            {
                Logger.LogError("Media file conversion failed: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
                WriteError(CreatePathErrorRecord(
                    new Exception($"Failed to convert media file: {resolvedInputPath}"),
                    "ConversionFailed",
                    ErrorCategory.OperationStopped,
                    resolvedInputPath));
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while converting media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            WriteError(CreatePathErrorRecord(ex, "ConversionFailed", ErrorCategory.OperationStopped, resolvedInputPath));
            return;
        }
    }
}

