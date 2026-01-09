using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Automatically converts multiple media files with intelligent audio stream selection.
/// </summary>
/// <remarks>
/// This cmdlet processes multiple video files, automatically detecting and configuring audio streams
/// based on codec type and channel count. It applies default video encoding settings (libx265, CRF 22, preset fast)
/// unless overridden, and provides a summary of any files that couldn't be processed.
/// </remarks>
[Cmdlet(VerbsData.Convert, "AutoMediaFiles")]
[OutputType(typeof(ConversionResult))]
public class ConvertAutoMediaFilesCommand : CmdletBase
{
    private static class HelpMessages
    {
        public const string InputPath = "Array of input file paths to convert";
        public const string OutputDirectory = "Directory where output files will be written (files keep original name with .mkv extension)";
        public const string VideoEncodingSettings = "Override default video encoding settings. If not provided, uses libx265, CRF 22, preset 'fast'";
    }

    /// <summary>
    /// Array of input file paths to convert. Can be passed via pipeline as strings or FileSystemInfo objects.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = HelpMessages.InputPath)]
    [ValidateNotNullOrEmpty]
    public object[] InputPath { get; set; } = Array.Empty<object>();

    /// <summary>
    /// Directory where output files will be written. Files keep original name with .mkv extension.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        HelpMessage = HelpMessages.OutputDirectory)]
    [ValidateNotNullOrEmpty]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Override default video encoding settings. If not provided, uses libx265, CRF 22, preset "fast".
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = HelpMessages.VideoEncodingSettings)]
    public VideoEncodingSettings? VideoEncodingSettings { get; set; }

    private IPathResolver? _pathResolver;
    private IMediaConversionService? _mediaConversionService;
    private IMediaReaderService? _mediaReaderService;
    private readonly List<ConversionResult> _conversionResults = new();
    private readonly HashSet<string> _uniqueInputPaths = new(StringComparer.OrdinalIgnoreCase);
    private int _currentFileIndex = 0;

    /// <summary>
    /// Path resolver service instance for resolving and validating file paths.
    /// </summary>
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    /// <summary>
    /// Media conversion service instance for performing conversions.
    /// </summary>
    private IMediaConversionService MediaConversionService => _mediaConversionService ??= ModuleServices.GetRequiredService<IMediaConversionService>();

    /// <summary>
    /// Media reader service instance for retrieving media file information.
    /// </summary>
    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    /// <summary>
    /// Initializes error tracking list and collects all input paths.
    /// </summary>
    protected override void Begin()
    {
        _conversionResults.Clear();
        _uniqueInputPaths.Clear();
        _currentFileIndex = 0;
    }

    /// <summary>
    /// Collects input paths from pipeline.
    /// </summary>
    protected override void Process()
    {
        if (InputPath == null || InputPath.Length == 0)
            return;

        foreach (var item in InputPath)
        {
            string path = item switch
            {
                string str => str,
                FileSystemInfo fsi => fsi.FullName,
                PSObject pso when pso.BaseObject is FileSystemInfo fsi => fsi.FullName,
                PSObject pso when pso.BaseObject is string str => str,
                _ => item.ToString() ?? throw new ArgumentException($"Cannot convert object of type {item.GetType()} to a file path", nameof(InputPath))
            };

            // Add path (HashSet automatically prevents duplicates with case-insensitive comparison)
            _uniqueInputPaths.Add(path);
        }
    }

    /// <summary>
    /// Processes all collected files and outputs summary table.
    /// </summary>
    protected override void End()
    {
        // Process all collected files
        if (_uniqueInputPaths.Count > 0)
        {
            var totalFiles = _uniqueInputPaths.Count;
            _currentFileIndex = 0;

            foreach (var inputPath in _uniqueInputPaths)
            {
                _currentFileIndex++;
                UpdateOverallProgress(_currentFileIndex, totalFiles, inputPath);
                ProcessFile(inputPath);
            }

            // Complete overall progress
            WriteProgress(new ProgressRecord(1, "Batch Conversion", "Completed") { RecordType = ProgressRecordType.Completed });
        }

        if (_conversionResults.Count == 0)
            return;

        // Output summary table
        var failedFiles = _conversionResults.Where(r => !r.Success).ToList();
        if (failedFiles.Count > 0)
        {
            WriteWarning($"{failedFiles.Count} file(s) could not be converted or had issues:");
            WriteObject(failedFiles, true);
        }

        // Output all results as objects for further processing
        WriteObject(_conversionResults, false);
    }

    /// <summary>
    /// Updates the overall progress for batch conversion.
    /// </summary>
    /// <param name="currentFile">Current file number (1-based).</param>
    /// <param name="totalFiles">Total number of files to process.</param>
    /// <param name="currentFilePath">Path of the current file being processed.</param>
    private void UpdateOverallProgress(int currentFile, int totalFiles, string currentFilePath)
    {
        var progressRecord = new ProgressRecord(1, "Batch Conversion", $"Processing file {currentFile} of {totalFiles} ({Path.GetFileName(currentFilePath)})")
        {
            PercentComplete = (int)((currentFile * 100.0) / totalFiles),
            CurrentOperation = Path.GetFileName(currentFilePath)
        };

        WriteProgress(progressRecord);
    }

    private void ProcessFile(string inputPath)
    {
        Logger.LogInformation("Processing file: {InputPath}", inputPath);

        // Resolve input path
        if (!PathResolver.TryResolveInputPath(inputPath, out var resolvedInputPath))
        {
            var result = new ConversionResult(inputPath, false, "File not found");
            _conversionResults.Add(result);
            WriteError(new ErrorRecord(
                new FileNotFoundException($"Input media file not found: {inputPath}"),
                "FileNotFound",
                ErrorCategory.ObjectNotFound,
                inputPath));
            return;
        }

        // Resolve output path
        var outputFileName = Path.GetFileNameWithoutExtension(resolvedInputPath) + ".mp4";
        var outputPath = Path.Combine(OutputDirectory, outputFileName);
        if (!PathResolver.TryResolveOutputPath(outputPath, out var resolvedOutputPath))
        {
            var result = new ConversionResult(inputPath, false, "Failed to resolve output path");
            _conversionResults.Add(result);
            WriteError(new ErrorRecord(
                new Exception($"Failed to resolve output path: {outputPath}"),
                "PathError",
                ErrorCategory.InvalidArgument,
                outputPath));
            return;
        }

        // Get media file info
        MediaFile? mediaFile;
        try
        {
            mediaFile = MediaReaderService.GetMediaFileAsync(resolvedInputPath, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read media file: {InputPath}", resolvedInputPath);
            var result = new ConversionResult(inputPath, false, $"Failed to read media file: {ex.Message}");
            _conversionResults.Add(result);
            WriteError(new ErrorRecord(ex, "MediaReadFailed", ErrorCategory.ReadError, resolvedInputPath));
            return;
        }

        if (mediaFile == null)
        {
            var result = new ConversionResult(inputPath, false, "Failed to read media file information");
            _conversionResults.Add(result);
            WriteWarning($"Could not read media file information for: {inputPath}");
            return;
        }

        // Check for audio streams
        var audioStreams = mediaFile.Streams
            .Where(s => string.Equals(s.Type, "audio", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // If no audio streams at all, process with empty mappings (video-only)
        if (audioStreams.Count == 0)
        {
            Logger.LogInformation("No audio streams found in: {InputPath}, processing as video-only", resolvedInputPath);
            ProcessConversion(resolvedInputPath, resolvedOutputPath, Array.Empty<AudioTrackMapping>(), inputPath);
            return;
        }

        // Filter for English streams only
        var englishAudioStreams = audioStreams
            .Where(s => string.Equals(s.Language, "eng", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // If no English streams but other audio streams exist, track as error
        if (englishAudioStreams.Count == 0)
        {
            Logger.LogInformation("No English audio streams found in: {InputPath}", resolvedInputPath);
            var result = new ConversionResult(inputPath, false, "No English audio streams found");
            _conversionResults.Add(result);
            WriteInformation(new InformationRecord($"No English audio streams found in: {inputPath}. Skipping file.", "NoEnglishAudioStreams"));
            return;
        }

        // Determine audio track mappings
        AudioTrackMapping[] audioMappings;
        try
        {
            audioMappings = CreateAudioTrackMappings(englishAudioStreams);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create audio track mappings for: {InputPath}", resolvedInputPath);
            var result = new ConversionResult(inputPath, false, $"Auto-detection failed: {ex.Message}");
            _conversionResults.Add(result);
            WriteWarning($"Audio settings can't be auto-detected for: {inputPath}. It must be processed manually. Error: {ex.Message}");
            return;
        }

        // Perform conversion
        ProcessConversion(resolvedInputPath, resolvedOutputPath, audioMappings, inputPath);
    }

    private AudioTrackMapping[] CreateAudioTrackMappings(List<MediaStream> englishAudioStreams)
    {
        var mappings = new List<AudioTrackMapping>();
        int destinationIndex = 0;

        foreach (var stream in englishAudioStreams)
        {
            int channels = AudioTrackMappingService.ParseChannelCount(stream.Raw);
            stream.Tags.TryGetValue("title", out var title);

            AudioTrackMapping mapping;
            var codecLower = stream.Codec.ToLowerInvariant();
            if ((codecLower == "dts" || codecLower == "truehd") && channels >= 6 && stream.Profile.ToLower() != "dts")
            {
                // DTS-HD MA or TrueHD: copy without re-encoding
                mapping = new CopyAudioTrackMapping(
                    title,
                    0,
                    stream.Index - 1,
                    destinationIndex);
            }
            else
            {
                // Other streams: encode as AAC, preserving channel count
                mapping = new EncodeAudioTrackMapping(
                    title,
                    0,
                    stream.Index - 1,
                    destinationIndex,
                    "aac",
                    0, // Bitrate 0 means use default based on channel count
                    channels);
            }

            mappings.Add(mapping);
            destinationIndex++;
        }

        // Apply swap logic: if first is DTS/TrueHD (copy) and second is multi-channel (6+ channels), swap destination indices
        if (mappings.Count >= 2 &&
            mappings[0] is CopyAudioTrackMapping copyMapping &&
            mappings[1] is EncodeAudioTrackMapping encodeMapping &&
            string.Equals(encodeMapping.DestinationCodec, "aac", StringComparison.OrdinalIgnoreCase) &&
            encodeMapping.DestinationChannels >= 6 && copyMapping.SourceIndex < encodeMapping.SourceIndex)
        {
            // Swap by creating new instances with swapped destination indices
            Logger.LogDebug("Applying swap logic: swapping destination indices for DTS/TrueHD and 6+ channel AAC");
            mappings[0] = new EncodeAudioTrackMapping(
                encodeMapping.Title,
                encodeMapping.SourceStream,
                encodeMapping.SourceIndex,
                copyMapping.DestinationIndex,
                encodeMapping.DestinationCodec,
                encodeMapping.DestinationBitrate,
                encodeMapping.DestinationChannels);

            mappings[1] = new CopyAudioTrackMapping(
                copyMapping.Title,
                copyMapping.SourceStream,
                copyMapping.SourceIndex,
                encodeMapping.DestinationIndex);
        }

        return mappings.ToArray();
    }

    private void ProcessConversion(string resolvedInputPath, string resolvedOutputPath, AudioTrackMapping[] audioMappings, string originalInputPath)
    {
        try
        {
            // Get or create video encoding settings
            var videoSettings = VideoEncodingSettings ?? CreateDefaultVideoEncodingSettings();

            Logger.LogDebug("Starting media file conversion: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);

            MediaConversionService.ExecuteConversion(
                resolvedInputPath,
                resolvedOutputPath,
                videoSettings,
                audioMappings,
                (progress, totalDurationMs, status) => ReportProgress(progress, totalDurationMs, status));

            // Complete progress reporting
            WriteProgress(new ProgressRecord(0, "Converting Media File", "Completed") { RecordType = ProgressRecordType.Completed });

            Logger.LogInformation("Successfully converted media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var result = new ConversionResult(originalInputPath, true, "Success");
            _conversionResults.Add(result);
        }
        catch (FfmpegConversionException ex)
        {
            Logger.LogError(ex, "FFmpeg conversion failed: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var statusMessage = BuildStatusMessage(ex);
            var result = new ConversionResult(originalInputPath, false, statusMessage);
            _conversionResults.Add(result);
            WriteError(new ErrorRecord(ex, "ConversionFailed", ErrorCategory.OperationStopped, resolvedInputPath));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while converting media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var result = new ConversionResult(originalInputPath, false, $"Conversion failed: {ex.Message}");
            _conversionResults.Add(result);
            WriteError(new ErrorRecord(ex, "ConversionFailed", ErrorCategory.OperationStopped, resolvedInputPath));
        }
    }

    private static string BuildStatusMessage(FfmpegConversionException ex)
    {
        var message = "Conversion failed";
        if (ex.ExitCode.HasValue)
            message += $" (exit code: {ex.ExitCode.Value})";
        if (!string.IsNullOrWhiteSpace(ex.ErrorOutput))
        {
            var errorLines = ex.ErrorOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (errorLines.Length > 0)
            {
                var firstErrorLine = errorLines[0].Trim();
                if (firstErrorLine.Length > 0)
                    message += $": {firstErrorLine}";
            }
        }
        return message;
    }


    private ConstantRateVideoEncodingSettings CreateDefaultVideoEncodingSettings()
    {
        return new ConstantRateVideoEncodingSettings(
            "libx265",
            "fast",
            "high",
            "film",
            22,
            VideoEncodingSettings.GetDefaultPixelFormat("libx265"));
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

    /// <summary>
    /// Represents the result of a conversion operation.
    /// </summary>
    public class ConversionResult
    {
        public ConversionResult(string filePath, bool success, string status)
        {
            FilePath = filePath;
            Success = success;
            Status = status;
        }

        /// <summary>
        /// Path to the input file that was processed.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Indicates whether the conversion was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Status message describing the result.
        /// </summary>
        public string Status { get; }
    }
}
