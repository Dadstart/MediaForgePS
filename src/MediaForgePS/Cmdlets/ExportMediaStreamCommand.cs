using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Extracts a single stream from a media file without re-encoding.
/// </summary>
/// <remarks>
/// -Type selects the stream kind (Video, Audio, Subtitle, Data, or All). -Index is zero-based within that type,
/// or the absolute stream index when Type is All. Use <see cref="GetMediaFileCommand"/> to inspect stream indices.
/// Supports -WhatIf and -Confirm. Use -Force to overwrite an existing output file.
/// </remarks>
[Cmdlet(VerbsData.Export, "MediaStream", SupportsShouldProcess = true)]
[OutputType(typeof(void))]
public class ExportMediaStreamCommand : CmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    /// <summary>
    /// Path to the input media file. Can be a relative or absolute path, and supports
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
    /// Path to the output file where the extracted stream will be saved.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        HelpMessage = "Path to the output file")]
    [ValidateNotNullOrEmpty]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Type of stream to extract: Video, Audio, Subtitle, Data, or All (any stream type).
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 2,
        HelpMessage = "Type of stream to extract")]
    [ValidateSet("Video", "Audio", "Subtitle", "Data", "All")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based index of the stream to extract within the specified type.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 3,
        HelpMessage = "Zero-based index of the stream to extract")]
    [ValidateRange(0, int.MaxValue)]
    public int Index { get; set; }

    /// <summary>
    /// Overwrites the output file if it already exists.
    /// </summary>
    [Parameter(HelpMessage = "Overwrites the output file if it already exists")]
    public SwitchParameter Force { get; set; }

    private IPathResolver? _pathResolver;
    private IFfmpegService? _ffmpegService;

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Ffmpeg service instance for extracting streams without re-encoding.
    /// </summary>
    private IFfmpegService FfmpegService => _ffmpegService ??= ModuleServices.GetRequiredService<IFfmpegService>();

    /// <summary>
    /// Processes the stream extraction request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Export-MediaStream request. InputPath: {InputPath}, OutputPath: {OutputPath}, Type: {Type}, Index: {Index}", InputPath, OutputPath, Type, Index);

        // Resolve input path
        if (!TryResolveInputPath(PathResolver, InputPath, out var resolvedInputPath))
            return;

        Logger.LogDebug("Resolved input path: {ResolvedInputPath}", resolvedInputPath);

        // Resolve output path
        if (!TryResolveOutputPath(PathResolver, OutputPath, out var resolvedOutputPath))
            return;

        Logger.LogDebug("Resolved output path: {ResolvedOutputPath}", resolvedOutputPath);

        // Check if output file exists and handle Force parameter
        if (!TryEnsureOutputCanBeWritten(resolvedOutputPath, Force.IsPresent))
            return;

        var ffmpegArguments = BuildStreamCopyArguments();
        Logger.LogDebug("FFmpeg arguments: {Arguments}", string.Join(" ", ffmpegArguments));

        var inputFileName = Path.GetFileName(resolvedInputPath);
        var outputFileName = Path.GetFileName(resolvedOutputPath);
        if (!ShouldProcess($"Extract stream from '{inputFileName}' to '{outputFileName}'", "Extract stream"))
        {
            Logger.LogInformation("WhatIf: Would extract stream from '{InputFileName}' to '{OutputFileName}'", inputFileName, outputFileName);
            return;
        }

        Logger.LogInformation("Executing FFmpeg to extract stream...");
        try
        {
            FfmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                ffmpegArguments,
                cancellationToken: StoppingToken,
                timeout: ProcessTimeouts.Extract,
                overwrite: Force.IsPresent).ConfigureAwait(false).GetAwaiter().GetResult();

            Logger.LogInformation("Successfully extracted stream to: {OutputFileName}", outputFileName);
        }
        catch (FfmpegConversionException ex)
        {
            Logger.LogError(ex, "Failed to extract stream");
            var errorId = ex.InnerException is not null
                ? ErrorIds.FfmpegExecutionException
                : ErrorIds.FfmpegExecutionFailed;
            WriteStandardError(ex, errorId, ErrorCategory.OperationStopped, resolvedOutputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while executing FFmpeg");
            WriteStandardError(ex, ErrorIds.FfmpegExecutionException, ErrorCategory.OperationStopped, resolvedOutputPath);
        }
    }

    private List<string> BuildStreamCopyArguments()
    {
        var arguments = new List<string>();

        if (Type.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("-map");
            arguments.Add($"0:{Index}");
        }
        else
        {
            var streamTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Video", "v" },
                { "Audio", "a" },
                { "Subtitle", "s" },
                { "Data", "d" }
            };

            if (streamTypeMap.TryGetValue(Type, out var streamType))
            {
                arguments.Add("-map");
                arguments.Add($"0:{streamType}:{Index}");
            }
            else
            {
                throw new ArgumentException($"Invalid stream type: {Type}", nameof(Type));
            }
        }

        arguments.Add("-c");
        arguments.Add("copy");

        return arguments;
    }
}
