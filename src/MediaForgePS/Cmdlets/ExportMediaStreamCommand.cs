using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Exports a specific stream from a media file to a separate file.
/// </summary>
/// <remarks>
/// This cmdlet uses ffmpeg to extract a specific stream (video, audio, subtitle, or data) from a media file
/// and save it to a separate output file. The stream is copied without re-encoding to preserve quality.
/// </remarks>
[Cmdlet(VerbsData.Export, "MediaStream", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(bool))]
public class ExportMediaStreamCommand : CmdletBase
{
    /// <summary>
    /// Media file object from which to export the stream. Can be provided via pipeline.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Media file object from which to export the stream")]
    [ValidateNotNull]
    public MediaFile MediaFile { get; set; } = null!;

    /// <summary>
    /// Path to the output file where the stream will be exported.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        HelpMessage = "Path to the output file")]
    [ValidateNotNullOrEmpty]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Type of stream to export: Video, Audio, Subtitle, Data, or All (for absolute index).
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 2,
        HelpMessage = "Type of stream to export: Video, Audio, Subtitle, Data, or All")]
    [ValidateSet("Video", "Audio", "Subtitle", "Data", "All")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Index of the stream to export (0-based). When Type is 'All', this refers to the absolute stream index.
    /// Otherwise, it refers to the index within streams of the specified type.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 3,
        HelpMessage = "Index of the stream to export (0-based)")]
    [ValidateRange(0, int.MaxValue)]
    public int Index { get; set; }

    /// <summary>
    /// Forces overwrite of the output file if it already exists.
    /// </summary>
    [Parameter(HelpMessage = "Forces overwrite of the output file if it already exists")]
    public SwitchParameter Force { get; set; }

    private IFfmpegService? _ffmpegService;
    private IPathResolver? _pathResolver;

    /// <summary>
    /// Ffmpeg service instance for executing stream export operations.
    /// </summary>
    private IFfmpegService FfmpegService => _ffmpegService ??= ModuleServices.GetRequiredService<IFfmpegService>();

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Processes the stream export request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Processing Export-Stream request for MediaFile: {Path}, Output: {OutputPath}, Type: {Type}, Index: {Index}",
            MediaFile.Path, OutputPath, Type, Index);

        // Resolve output path
        string resolvedOutputPath;
        if (!PathResolver.TryResolveOutputPath(OutputPath, out resolvedOutputPath))
        {
            var errorRecord = new ErrorRecord(
                new Exception($"Failed to resolve output path: {OutputPath}"),
                "PathError",
                ErrorCategory.InvalidArgument,
                OutputPath);
            WriteError(errorRecord);
            return;
        }

        // Check if output file exists and handle Force parameter
        if (File.Exists(resolvedOutputPath))
        {
            if (Force)
            {
                Logger.LogWarning("Output file exists and Force specified. Will overwrite: {OutputPath}", resolvedOutputPath);
            }
            else
            {
                Logger.LogError("Output file already exists: {OutputPath}. Use -Force to overwrite.", resolvedOutputPath);
                var errorRecord = new ErrorRecord(
                    new IOException($"Output file already exists: {resolvedOutputPath}. Use -Force to overwrite."),
                    "FileExists",
                    ErrorCategory.ResourceExists,
                    resolvedOutputPath);
                WriteError(errorRecord);
                return;
            }
        }

        // Build FFmpeg arguments for stream extraction
        var ffmpegArguments = BuildFfmpegArguments();

        // Get file names for ShouldProcess message
        var inputFileName = Path.GetFileName(MediaFile.Path);
        var outputFileName = Path.GetFileName(resolvedOutputPath);

        // Execute with ShouldProcess support
        var actionMessage = $"Extract {Type} stream {Index} from '{inputFileName}' to '{outputFileName}'";
        if (ShouldProcess(actionMessage, "Extract stream"))
        {
            Logger.LogDebug("Executing FFmpeg to extract stream with arguments: {Arguments}", string.Join(" ", ffmpegArguments));

            try
            {
                // Note: Using GetAwaiter().GetResult() to synchronously wait for the async operation
                // This is acceptable in PowerShell cmdlets which must be synchronous
                var success = FfmpegService.ConvertAsync(MediaFile.Path, resolvedOutputPath, ffmpegArguments, CancellationToken.None)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (success)
                {
                    Logger.LogInformation("Successfully extracted stream to: {OutputPath}", resolvedOutputPath);
                    WriteObject(true);
                }
                else
                {
                    Logger.LogError("Failed to extract stream. FFmpeg conversion returned false.");
                    var errorRecord = new ErrorRecord(
                        new Exception($"Failed to extract stream to: {resolvedOutputPath}"),
                        "StreamExportFailed",
                        ErrorCategory.OperationStopped,
                        resolvedOutputPath);
                    WriteError(errorRecord);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception occurred while extracting stream: {OutputPath}", resolvedOutputPath);
                var errorRecord = new ErrorRecord(
                    ex,
                    "StreamExportFailed",
                    ErrorCategory.OperationStopped,
                    resolvedOutputPath);
                WriteError(errorRecord);
            }
        }
        else
        {
            Logger.LogDebug("WhatIf: Would extract {Type} stream {Index} from '{inputFileName}' to '{outputFileName}'", Type, Index, inputFileName, outputFileName);
        }
    }

    /// <summary>
    /// Builds the FFmpeg arguments for stream extraction based on Type and Index parameters.
    /// </summary>
    /// <returns>List of FFmpeg arguments.</returns>
    private List<string> BuildFfmpegArguments()
    {
        var arguments = new List<string>();

        // Add stream mapping based on type and index
        if (Type == "All")
        {
            // Extract by absolute stream index
            arguments.Add("-map");
            arguments.Add($"0:{Index}");
        }
        else
        {
            // Extract by stream type and index
            var streamTypeMap = new Dictionary<string, string>
            {
                { "Video", "v" },
                { "Audio", "a" },
                { "Subtitle", "s" },
                { "Data", "d" }
            };

            if (!streamTypeMap.TryGetValue(Type, out var streamType))
            {
                throw new ArgumentException($"Invalid stream type: {Type}", nameof(Type));
            }

            arguments.Add("-map");
            arguments.Add($"0:{streamType}:{Index}");
        }

        // Copy stream without re-encoding
        arguments.Add("-c");
        arguments.Add("copy");

        return arguments;
    }
}
