using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Base class for media conversion cmdlets that provides common functionality for building
/// Ffmpeg arguments and performing conversions.
/// </summary>
public abstract class ConvertMediaCommandBase : CmdletBase
{
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
    protected IFfmpegService FfmpegService => _ffmpegService ??= ModuleServices.GetRequiredService<IFfmpegService>();

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    protected IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Platform service instance for platform-specific operations.
    /// </summary>
    protected IPlatformService PlatformService => _platformService ??= ModuleServices.GetRequiredService<IPlatformService>();

    /// <summary>
    /// Builds the Ffmpeg arguments from video encoding settings, audio track mappings, and additional arguments.
    /// </summary>
    /// <param name="pass">The encoding pass number (1 or 2 for two-pass, null for single-pass).</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    protected IEnumerable<string> BuildFfmpegArguments(int? pass)
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
    /// Creates an error record for a path resolution error.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="errorId">The error ID.</param>
    /// <param name="errorCategory">The error category.</param>
    /// <param name="targetObject">The target object that caused the error.</param>
    /// <returns>An ErrorRecord for the path resolution error.</returns>
    protected ErrorRecord CreatePathErrorRecord(Exception exception, string errorId, ErrorCategory errorCategory, object targetObject)
    {
        return new ErrorRecord(
            exception,
            errorId,
            errorCategory,
            targetObject);
    }

    /// <summary>
    /// Writes a path error record to the error stream.
    /// </summary>
    /// <param name="path">The path that caused the error.</param>
    /// <param name="message">The error message.</param>
    protected void WritePathErrorRecord(string path, string message)
    {
        WriteError(CreatePathErrorRecord(new Exception(message), "PathError", ErrorCategory.InvalidArgument, path));
    }

    /// <summary>
    /// Performs the media file conversion using Ffmpeg.
    /// </summary>
    /// <param name="inputPath">The resolved input file path.</param>
    /// <param name="outputPath">The resolved output file path.</param>
    /// <returns>True if the conversion was successful, false otherwise.</returns>
    protected bool ConvertMediaFile(string inputPath, string outputPath)
    {
        Logger.LogDebug("Starting media file conversion: {InputPath} -> {OutputPath}", inputPath, outputPath);

        bool success;
        if (VideoEncodingSettings.IsSinglePass)
        {
            success = FfmpegService.ConvertAsync(inputPath, outputPath, BuildFfmpegArguments(null), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        else
        {
            success = FfmpegService.ConvertAsync(inputPath, outputPath, BuildFfmpegArguments(1), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult()
                && FfmpegService.ConvertAsync(inputPath, outputPath, BuildFfmpegArguments(2), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        return success;
    }
}
