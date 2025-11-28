using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Microsoft.Extensions.DependencyInjection;
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
    /// Additional Ffmpeg arguments to pass to the conversion process.
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Additional Ffmpeg arguments (e.g., codec options, quality settings)")]
    public string[]? Arguments { get; set; }

    private IFfmpegService? _ffmpegService;

    /// <summary>
    /// Ffmpeg service instance for performing media file conversion.
    /// </summary>
    private IFfmpegService FfmpegService => _ffmpegService ??= ModuleServices.GetRequiredService<IFfmpegService>();

    /// <summary>
    /// Processes the media file conversion request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Convert-MediaFile request: {InputPath} -> {OutputPath}", InputPath, OutputPath);

        string resolvedInputPath;
        try
        {
            // Resolve PowerShell path (handles wildcards, provider paths, relative paths, etc.)
            Logger.LogDebug("Resolving PowerShell input path: {InputPath}", InputPath);
            var providerPaths = GetResolvedProviderPathFromPSPath(InputPath, out var provider);
            if (providerPaths.Count == 0)
            {
                Logger.LogWarning("Input path resolution returned no results for: {InputPath}", InputPath);
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"Input media file not found: {InputPath}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    InputPath);
                WriteError(errorRecord);
                return;
            }
            resolvedInputPath = providerPaths[0];
            Logger.LogDebug("Resolved input path: {ResolvedInputPath}", resolvedInputPath);

            // If the resolved path is the same as the input path and the file doesn't exist,
            // it means the path couldn't be resolved (file not found)
            if (resolvedInputPath.Equals(InputPath, StringComparison.OrdinalIgnoreCase) && !File.Exists(resolvedInputPath))
            {
                Logger.LogWarning("Input path could not be resolved and file does not exist: {InputPath}", InputPath);
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"Input media file not found: {InputPath}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    InputPath);
                WriteError(errorRecord);
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resolve input path: {InputPath}", InputPath);

            var fileNotFoundException = new FileNotFoundException($"Input media file not found: {InputPath}");
            var errorRecordToWrite = new ErrorRecord(
                fileNotFoundException,
                "FileNotFound",
                ErrorCategory.ObjectNotFound,
                InputPath);

            ThrowTerminatingError(errorRecordToWrite);
            return;
        }

        string resolvedOutputPath;
        try
        {
            // Resolve output path (may not exist yet, but directory should exist or be creatable)
            Logger.LogDebug("Resolving PowerShell output path: {OutputPath}", OutputPath);
            var providerPaths = GetResolvedProviderPathFromPSPath(OutputPath, out var provider);
            if (providerPaths.Count > 0)
            {
                resolvedOutputPath = providerPaths[0];
            }
            else
            {
                // If path resolution fails, try to use the path as-is (might be a new file)
                resolvedOutputPath = OutputPath;
            }
            Logger.LogDebug("Resolved output path: {ResolvedOutputPath}", resolvedOutputPath);

            // Ensure the output directory exists
            var outputDirectory = Path.GetDirectoryName(resolvedOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Logger.LogInformation("Creating output directory: {OutputDirectory}", outputDirectory);
                Directory.CreateDirectory(outputDirectory);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resolve output path: {OutputPath}", OutputPath);

            var errorRecord = new ErrorRecord(
                ex,
                "OutputPathInvalid",
                ErrorCategory.InvalidArgument,
                OutputPath);
            ThrowTerminatingError(errorRecord);
            return;
        }

        try
        {
            // Verify the input file exists before attempting conversion
            Logger.LogDebug("Checking if input file exists: {ResolvedInputPath}", resolvedInputPath);
            if (!File.Exists(resolvedInputPath))
            {
                Logger.LogWarning("Input file does not exist: {ResolvedInputPath}", resolvedInputPath);
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"Input media file not found: {resolvedInputPath}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    resolvedInputPath);
                WriteError(errorRecord);
                return;
            }

            // Perform the conversion
            // Note: Using GetAwaiter().GetResult() to synchronously wait for the async operation
            // This is acceptable in PowerShell cmdlets which must be synchronous
            Logger.LogDebug("Starting media file conversion: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var success = FfmpegService.ConvertAsync(resolvedInputPath, resolvedOutputPath, Arguments).ConfigureAwait(false).GetAwaiter().GetResult();

            if (success)
            {
                Logger.LogInformation("Successfully converted media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
                WriteObject(true);
            }
            else
            {
                Logger.LogError("Media file conversion failed: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
                var errorRecord = new ErrorRecord(
                    new Exception($"Failed to convert media file: {resolvedInputPath}"),
                    "ConversionFailed",
                    ErrorCategory.OperationStopped,
                    resolvedInputPath);
                WriteError(errorRecord);
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while converting media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var errorRecord = new ErrorRecord(
                ex,
                "ConversionFailed",
                ErrorCategory.OperationStopped,
                resolvedInputPath);
            WriteError(errorRecord);
            return;
        }
    }
}

