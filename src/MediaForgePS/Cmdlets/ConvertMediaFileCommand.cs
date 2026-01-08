using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
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
public class ConvertMediaFileCommand : CmdletBase
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
    /// Video encoding settings to use for the conversion.
    /// </summary>
    [Parameter(
        Mandatory = true,
        HelpMessage = "Video encoding settings to use for the conversion")]
    public VideoEncodingSettings VideoEncodingSettings { get; set; } = null!;

    /// <summary>
    /// Audio track mappings to use for the conversion.
    /// </summary>
    [Parameter(
        Mandatory = true,
        HelpMessage = "Audio track mappings to use for the conversion")]
    public AudioTrackMapping[] AudioTrackMappings { get; set; } = Array.Empty<AudioTrackMapping>();

    /// <summary>
    /// Additional Ffmpeg arguments to pass to the conversion process.
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Additional Ffmpeg arguments (e.g., codec options, quality settings)")]
    public string[]? AdditionalArguments { get; set; }

    private IPathResolver? _pathResolver;
    private IMediaConversionService? _mediaConversionService;

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Media conversion service instance for performing conversions.
    /// </summary>
    private IMediaConversionService MediaConversionService => _mediaConversionService ??= ModuleServices.GetRequiredService<IMediaConversionService>();


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
    /// Creates an error record for a path resolution error.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="errorId">The error ID.</param>
    /// <param name="errorCategory">The error category.</param>
    /// <param name="targetObject">The target object that caused the error.</param>
    /// <returns>An ErrorRecord for the path resolution error.</returns>
    private ErrorRecord CreatePathErrorRecord(Exception exception, string errorId, ErrorCategory errorCategory, object targetObject)
    {
        return new ErrorRecord(
            exception,
            errorId,
            errorCategory,
            targetObject);
    }

    private void WritePathErrorRecord(string path, string message)
    {
        WriteError(CreatePathErrorRecord(new Exception(message), "PathError", ErrorCategory.InvalidArgument, path));
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
            Logger.LogDebug("Starting media file conversion: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);

            bool success = MediaConversionService.ExecuteConversion(
                resolvedInputPath,
                resolvedOutputPath,
                VideoEncodingSettings,
                AudioTrackMappings,
                (progress, totalDurationMs, status) => ReportProgress(progress, totalDurationMs, status),
                AdditionalArguments);

            // Complete progress reporting
            WriteProgress(new ProgressRecord(0, "Converting Media File", "Completed") { RecordType = ProgressRecordType.Completed });

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

    /// <summary>
    /// Reports progress using PowerShell's WriteProgress cmdlet.
    /// </summary>
    /// <param name="progress">The Ffmpeg progress information.</param>
    /// <param name="totalDurationMs">Total duration of the input file in milliseconds, if available.</param>
    /// <param name="status">Status message to display.</param>
    private void ReportProgress(FfmpegProgress progress, long? totalDurationMs, string status)
    {
        var progressRecord = MediaConversionHelper.CreateProgressRecord(progress, totalDurationMs, status);
        WriteProgress(progressRecord);
    }
}

