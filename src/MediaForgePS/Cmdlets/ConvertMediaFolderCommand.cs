using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts media files in a folder matching a specified file filter using Ffmpeg.
/// </summary>
/// <remarks>
/// This cmdlet processes all files in a folder that match the specified filter pattern,
/// converting each file using the provided video encoding settings and audio track mappings.
/// </remarks>
[Cmdlet(VerbsData.Convert, "MediaFolder")]
[OutputType(typeof(bool))]
public class ConvertMediaFolderCommand : CmdletBase
{
    /// <summary>
    /// Path to the input folder containing media files to convert. Can be a relative or absolute path,
    /// and supports PowerShell path resolution including wildcards and provider paths.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to the input folder containing media files")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// File filter pattern to match files in the input folder (e.g., '*.mkv', '*.mp4').
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        HelpMessage = "File filter pattern to match files (e.g., '*.mkv', '*.mp4')")]
    [ValidateNotNullOrEmpty]
    public string Filter { get; set; } = string.Empty;

    /// <summary>
    /// Path to the output folder where converted files will be saved. Can be a relative or absolute path.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 2,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to the output folder for converted files")]
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

    private IFfmpegService? _ffmpegService;
    private IPathResolver? _pathResolver;
    private IPlatformService? _platformService;

    /// <summary>
    /// Ffmpeg service instance for performing media file conversion.
    /// </summary>
    private IFfmpegService FfmpegService => _ffmpegService ??= ModuleServices.GetRequiredService<IFfmpegService>();

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Platform service instance for platform-specific operations.
    /// </summary>
    private IPlatformService PlatformService => _platformService ??= ModuleServices.GetRequiredService<IPlatformService>();

    /// <summary>
    /// Builds the Ffmpeg arguments from video encoding settings, audio track mappings, and additional arguments.
    /// </summary>
    /// <param name="pass">The encoding pass number (1 or 2 for two-pass, null for single-pass).</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    private IEnumerable<string> BuildFfmpegArguments(int? pass)
    {
        var args = new List<string>();

        // Add video encoding arguments
        args.AddRange(VideoEncodingSettings.ToFfmpegArgs(PlatformService, pass));

        // Add audio track mapping arguments
        foreach (var audioMapping in AudioTrackMappings)
        {
            args.AddRange(audioMapping.ToFfmpegArgs(PlatformService));
        }

        // Add additional arguments if provided
        if (AdditionalArguments != null)
        {
            args.AddRange(AdditionalArguments);
        }

        return args;
    }

    /// <summary>
    /// Creates an error record for a directory not found error.
    /// </summary>
    /// <param name="path">The path that was not found.</param>
    /// <param name="message">The error message.</param>
    /// <returns>An ErrorRecord for the directory not found error.</returns>
    private ErrorRecord CreateDirectoryNotFoundErrorRecord(string path, string message)
    {
        return new ErrorRecord(
            new DirectoryNotFoundException(message),
            "DirectoryNotFound",
            ErrorCategory.ObjectNotFound,
            path);
    }

    private void WriteDirectoryNotFoundErrorRecord(string path, string message)
    {
        WriteError(CreateDirectoryNotFoundErrorRecord(path, message));
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
    /// Resolves a PowerShell folder path to an absolute path.
    /// </summary>
    /// <param name="folderPath">The folder path to resolve.</param>
    /// <param name="resolvedPath">The resolved absolute path.</param>
    /// <returns>True if the path was resolved successfully, false otherwise.</returns>
    private bool TryResolveFolderPath(string folderPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        try
        {
            Logger.LogDebug("Resolving PowerShell folder path: {FolderPath}", folderPath);

            if (!Dadstart.Labs.MediaForge.Services.System.PathResolver.TryResolveProviderPath(this, folderPath, out var providerResolvedPath))
            {
                Logger.LogWarning("Folder path resolution returned no results for: {FolderPath}", folderPath);
                return false;
            }

            resolvedPath = providerResolvedPath!;
            Logger.LogDebug("Resolved folder path: {ResolvedFolderPath}", resolvedPath);

            // Validate that the resolved path is a directory
            if (!Directory.Exists(resolvedPath))
            {
                Logger.LogWarning("Resolved folder path does not exist: {ResolvedFolderPath}", resolvedPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resolve folder path: {FolderPath}", folderPath);
            return false;
        }
    }

    /// <summary>
    /// Processes the media folder conversion request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Convert-MediaFolder request: {InputFolder} (filter: {FileFilter}) -> {OutputFolder}", Path, Filter, OutputPath);

        // Resolve input folder path
        string resolvedInputFolder;
        if (!TryResolveFolderPath(Path, out resolvedInputFolder))
        {
            WriteDirectoryNotFoundErrorRecord(Path, $"Input folder not found: {Path}");
            return;
        }

        // Resolve output folder path
        string resolvedOutputFolder;
        if (!TryResolveFolderPath(OutputPath, out resolvedOutputFolder))
        {
            // If output folder doesn't exist, try to resolve it using PowerShell path resolution
            if (!Dadstart.Labs.MediaForge.Services.System.PathResolver.TryResolveProviderPath(this, OutputPath, out var providerResolvedPath))
            {
                // If path resolution fails, try to use the path as-is (might be a new folder)
                resolvedOutputFolder = OutputPath;
            }
            else
            {
                resolvedOutputFolder = providerResolvedPath!;
            }

            // Ensure the output directory exists
            if (!Directory.Exists(resolvedOutputFolder))
            {
                Logger.LogInformation("Creating output directory: {ResolvedOutputFolder}", resolvedOutputFolder);
                Directory.CreateDirectory(resolvedOutputFolder);
            }
        }

        try
        {
            // Find all files matching the filter in the input folder
            Logger.LogDebug("Searching for files matching filter '{FileFilter}' in folder: {ResolvedInputFolder}", Filter, resolvedInputFolder);
            var matchingFiles = Directory.GetFiles(resolvedInputFolder, Filter, SearchOption.TopDirectoryOnly);

            if (matchingFiles.Length == 0)
            {
                Logger.LogWarning("No files found matching filter '{FileFilter}' in folder: {ResolvedInputFolder}", Filter, resolvedInputFolder);
                WriteObject(false);
                return;
            }

            Logger.LogInformation("Found {FileCount} file(s) matching filter '{FileFilter}' in folder: {ResolvedInputFolder}", matchingFiles.Length, Filter, resolvedInputFolder);

            // Process each matching file
            int successCount = 0;
            int failureCount = 0;

            foreach (var inputFilePath in matchingFiles.OrderBy(f => f))
            {
                var inputFileName = System.IO.Path.GetFileName(inputFilePath);
                var outputFilePath = System.IO.Path.Combine(resolvedOutputFolder, inputFileName);

                Logger.LogInformation("Converting file: {InputFileName} -> {OutputFilePath}", inputFileName, outputFilePath);

                try
                {
                    // Perform the conversion
                    // Note: Using GetAwaiter().GetResult() to synchronously wait for the async operation
                    // This is acceptable in PowerShell cmdlets which must be synchronous
                    Logger.LogDebug("Starting media file conversion: {InputFilePath} -> {OutputFilePath}", inputFilePath, outputFilePath);
                    if (File.Exists(outputFilePath))
                    {
                        Logger.LogWarning("Output file already exists: {OutputFilePath}", outputFilePath);
                        continue;
                    }

                    bool success;
                    if (VideoEncodingSettings.IsSinglePass)
                    {
                        success = FfmpegService.ConvertAsync(inputFilePath, outputFilePath, BuildFfmpegArguments(null), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    else
                    {
                        success = FfmpegService.ConvertAsync(inputFilePath, outputFilePath, BuildFfmpegArguments(1), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult()
                            && FfmpegService.ConvertAsync(inputFilePath, outputFilePath, BuildFfmpegArguments(2), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                    }

                    if (success)
                    {
                        Logger.LogInformation("Successfully converted media file: {InputFilePath} -> {OutputFilePath}", inputFilePath, outputFilePath);
                        successCount++;
                    }
                    else
                    {
                        Logger.LogError("Media file conversion failed: {InputFilePath} -> {OutputFilePath}", inputFilePath, outputFilePath);
                        WriteError(CreatePathErrorRecord(
                            new Exception($"Failed to convert media file: {inputFilePath}"),
                            "ConversionFailed",
                            ErrorCategory.OperationStopped,
                            inputFilePath));
                        failureCount++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Exception occurred while converting media file: {InputFilePath} -> {OutputFilePath}", inputFilePath, outputFilePath);
                    WriteError(CreatePathErrorRecord(ex, "ConversionFailed", ErrorCategory.OperationStopped, inputFilePath));
                    failureCount++;
                }
            }

            // Output overall result
            bool overallSuccess = failureCount == 0;
            Logger.LogInformation("Folder conversion completed: {SuccessCount} succeeded, {FailureCount} failed", successCount, failureCount);
            WriteObject(overallSuccess);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while processing folder conversion: {ResolvedInputFolder} -> {ResolvedOutputFolder}", resolvedInputFolder, resolvedOutputFolder);
            WriteError(CreatePathErrorRecord(ex, "FolderConversionFailed", ErrorCategory.OperationStopped, resolvedInputFolder));
            return;
        }
    }
}
