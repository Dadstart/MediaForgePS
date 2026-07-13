using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
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
[Cmdlet(VerbsData.Export, "MediaStream", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
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
    private IExecutableService? _executableService;

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Executable service instance for executing FFmpeg.
    /// </summary>
    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();

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
        if (File.Exists(resolvedOutputPath))
        {
            if (Force)
            {
                Logger.LogWarning("Output file exists and Force specified. Will overwrite: {ResolvedOutputPath}", resolvedOutputPath);
            }
            else
            {
                var errorMessage = $"Output file already exists: {resolvedOutputPath}. Use -Force to overwrite.";
                Logger.LogError(errorMessage);
                var errorRecord = new ErrorRecord(
                    new IOException(errorMessage),
                    ErrorIds.OutputFileExists,
                    ErrorCategory.ResourceExists,
                    resolvedOutputPath);
                WriteError(errorRecord);
                return;
            }
        }

        // Build FFmpeg arguments
        var ffmpegArguments = BuildFfmpegArguments(resolvedInputPath, resolvedOutputPath);
        Logger.LogDebug("FFmpeg arguments: {Arguments}", string.Join(" ", ffmpegArguments));

        // Get file names for ShouldProcess message
        var inputFileName = Path.GetFileName(resolvedInputPath);
        var outputFileName = Path.GetFileName(resolvedOutputPath);
        var shouldProcessMessage = $"Extract stream from '{inputFileName}' to '{outputFileName}'";
        var shouldProcessCaption = "Extract stream";

        // Execute FFmpeg with ShouldProcess support
        if (ShouldProcess(shouldProcessMessage, shouldProcessCaption))
        {
            Logger.LogInformation("Executing FFmpeg to extract stream...");
            try
            {
                var result = ExecutableService.ExecuteAsync("ffmpeg", ffmpegArguments, StoppingToken).ConfigureAwait(false).GetAwaiter().GetResult();

                if (result.ExitCode == 0)
                {
                    Logger.LogInformation("Successfully extracted stream to: {OutputFileName}", outputFileName);
                }
                else
                {
                    var errorMessage = $"Failed to extract stream. FFmpeg exit code: {result.ExitCode}";
                    if (!string.IsNullOrWhiteSpace(result.ErrorOutput))
                        errorMessage += $". Error: {result.ErrorOutput}";

                    Logger.LogError(errorMessage);
                    var errorRecord = new ErrorRecord(
                        new Exception(errorMessage),
                        ErrorIds.FfmpegExecutionFailed,
                        ErrorCategory.OperationStopped,
                        null);
                    WriteError(errorRecord);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception occurred while executing FFmpeg");
                var errorRecord = new ErrorRecord(
                    ex,
                    ErrorIds.FfmpegExecutionException,
                    ErrorCategory.OperationStopped,
                    null);
                WriteError(errorRecord);
            }
        }
        else
        {
            Logger.LogInformation("WhatIf: Would extract stream from '{InputFileName}' to '{OutputFileName}'", inputFileName, outputFileName);
        }
    }

    private List<string> BuildFfmpegArguments(string inputPath, string outputPath)
    {
        var arguments = new List<string>();

        // Input file
        arguments.Add("-i");
        arguments.Add(inputPath);

        // Stream mapping based on type and index
        if (Type.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            // Extract by absolute stream index
            arguments.Add("-map");
            arguments.Add($"0:{Index}");
        }
        else
        {
            // Extract by stream type and index
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

        // Copy stream without re-encoding
        arguments.Add("-c");
        arguments.Add("copy");

        // Overwrite output file if Force is specified (we've already checked file existence)
        if (Force)
            arguments.Add("-y");

        // Output file
        arguments.Add(outputPath);

        return arguments;
    }
}
