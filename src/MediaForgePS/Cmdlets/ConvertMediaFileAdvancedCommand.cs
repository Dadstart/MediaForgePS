using System;
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
[Cmdlet(VerbsData.Convert, "MediaFileAdvanced")]
[OutputType(typeof(bool))]
public class ConvertMediaFileAdvancedCommand : CmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

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

    /// <summary>
    /// Additional x265 params to pass through to ffmpeg.
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Additional x265 params (passed to ffmpeg via -x265-params)")]
    public string? X265Params { get; set; }

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
    /// Processes the media file conversion request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Convert-MediaFileAdvanced request: {InputPath} -> {OutputPath}", InputPath, OutputPath);

        if (!TryResolveInputPath(PathResolver, InputPath, out var resolvedInputPath))
            return;

        if (!TryResolveOutputPath(PathResolver, OutputPath, out var resolvedOutputPath))
            return;

        try
        {
            // Perform the conversion
            // Note: Using GetAwaiter().GetResult() to synchronously wait for the async operation
            // This is acceptable in PowerShell cmdlets which must be synchronous
            Logger.LogDebug("Starting media file conversion: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var x265Arguments = MediaConversionHelper.BuildX265Arguments(X265Params, VideoEncodingSettings.Codec);
            var additionalArguments = MergeAdditionalArguments(AdditionalArguments, x265Arguments);

            MediaConversionService.ExecuteConversion(
                resolvedInputPath,
                resolvedOutputPath,
                VideoEncodingSettings,
                AudioTrackMappings,
                additionalArguments);

            Logger.LogInformation("Successfully converted media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
        }
        catch (FfmpegConversionException ex)
        {
            Logger.LogError(ex, "FFmpeg conversion failed: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            WriteStandardError(ex, ErrorIds.ConversionFailed, ErrorCategory.OperationStopped, resolvedInputPath);
            return;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while converting media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            WriteStandardError(ex, ErrorIds.ConversionFailed, ErrorCategory.OperationStopped, resolvedInputPath);
            return;
        }
    }

    private static string[]? MergeAdditionalArguments(string[]? additionalArguments, string[]? x265Arguments)
    {
        if (additionalArguments is null || additionalArguments.Length == 0)
            return x265Arguments;

        if (x265Arguments is null || x265Arguments.Length == 0)
            return additionalArguments;

        var merged = new string[additionalArguments.Length + x265Arguments.Length];
        Array.Copy(additionalArguments, merged, additionalArguments.Length);
        Array.Copy(x265Arguments, 0, merged, additionalArguments.Length, x265Arguments.Length);
        return merged;
    }

}
