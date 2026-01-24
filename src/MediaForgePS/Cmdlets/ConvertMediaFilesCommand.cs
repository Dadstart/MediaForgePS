using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Represents statistics for a processed file used for ETA calculations.
/// </summary>
internal class FileProcessingStats
{
    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Time taken to process the file.
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Processing speed in bytes per second.
    /// </summary>
    public double BytesPerSecond => FileSizeBytes > 0 && ProcessingTime.TotalSeconds > 0
        ? FileSizeBytes / ProcessingTime.TotalSeconds
        : 0;
}

/// <summary>
/// Automatically converts multiple media files with intelligent audio stream selection.
/// </summary>
/// <remarks>
/// This cmdlet processes multiple video files, automatically detecting and configuring audio streams
/// based on codec type and channel count. It applies default video encoding settings (libx265, CRF 22, preset fast)
/// unless overridden, and provides a summary of any files that couldn't be processed.
/// Audio track mappings can be provided via the AudioTrackMappings parameter; if not provided, they are
/// automatically detected and created for each file.
/// </remarks>
[Cmdlet(VerbsData.Convert, "MediaFiles")]
[OutputType(typeof(ConversionResult))]
public class ConvertMediaFilesCommand : CmdletBase
{
    private const int BatchProgressId = 1;
    private const int FileProgressId = 2;
    private static class HelpMessages
    {
        public const string InputPath = "Array of input file paths to convert";
        public const string OutputDirectory = "Directory where output files will be written (files keep original name with .mkv extension)";
        public const string VideoEncodingSettings = "Override default video encoding settings. If not provided, uses libx265, CRF 22, preset 'fast'";
        public const string AudioTrackMappings = "Audio track mappings to use for all files. If not provided, mappings are automatically detected and created for each file";
        public const string X265Params = "Additional x265 params (passed to ffmpeg via -x265-params)";
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

    /// <summary>
    /// Audio track mappings to use for all files. If not provided, mappings are automatically detected and created for each file.
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = HelpMessages.AudioTrackMappings)]
    public AudioTrackMapping[] AudioTrackMappings { get; set; } = Array.Empty<AudioTrackMapping>();

    /// <summary>
    /// Additional x265 params to pass through to ffmpeg.
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = HelpMessages.X265Params)]
    public string? X265Params { get; set; }

    private IPathResolver? _pathResolver;
    private IMediaConversionService? _mediaConversionService;
    private IMediaReaderService? _mediaReaderService;
    private readonly List<ConversionResult> _conversionResults = new();
    private readonly HashSet<string> _uniqueInputPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileProcessingStats> _fileProcessingStats = new();
    private int _currentFileIndex = 0;
    private Stopwatch? _fileProcessingStopwatch;
    private TimeSpan? _currentFileEstimatedTime;

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
        _fileProcessingStats.Clear();
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
            WriteProgress(MediaConversionHelper.CreateSimpleProgressRecord(
                BatchProgressId,
                "Batch Conversion",
                "Completed",
                recordType: ProgressRecordType.Completed));
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
        var progressRecord = MediaConversionHelper.CreateSimpleProgressRecord(
            BatchProgressId,
            "Batch Conversion",
            $"Processing file {currentFile} of {totalFiles} ({Path.GetFileName(currentFilePath)})",
            percentComplete: (int)((currentFile * 100.0) / totalFiles));
        progressRecord.CurrentOperation = Path.GetFileName(currentFilePath);

        var remainingFiles = totalFiles - currentFile;
        if (remainingFiles > 0)
        {
            var batchEtaTimespan = CalculateRemainingTime(currentFilePath, remainingFiles);
            if (batchEtaTimespan.HasValue)
                progressRecord.StatusDescription += $" (Total ETA: {FormatTimespan(batchEtaTimespan.Value)})";
        }

        WriteProgress(progressRecord);
    }

    /// <summary>
    /// Calculates the estimated remaining time based on file sizes and average processing speed.
    /// </summary>
    /// <param name="currentFilePath">Path of the current file being processed.</param>
    /// <param name="remainingFilesCount">Number of remaining files after the current one.</param>
    /// <returns>Estimated time remaining, or null if no estimate can be calculated.</returns>
    private TimeSpan? CalculateRemainingTime(string currentFilePath, int remainingFilesCount)
    {
        if (_fileProcessingStats.Count == 0)
            return null;

        double averageBytesPerSecond = _fileProcessingStats.Average(s => s.BytesPerSecond);
        if (averageBytesPerSecond <= 0)
            return null;

        long remainingBytes = 0;

        try
        {
            var currentFile = new FileInfo(currentFilePath);
            if (currentFile.Exists)
                remainingBytes = currentFile.Length;
        }
        catch
        {
            // If we can't get the file size, skip ETA calculation
            return null;
        }

        // Add remaining files from the input paths list
        var remainingPaths = _uniqueInputPaths.Skip(_currentFileIndex).Take(remainingFilesCount);
        foreach (var path in remainingPaths)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                    remainingBytes += fileInfo.Length;
            }
            catch
            {
                // If we can't get the file size, continue with what we have
            }
        }

        if (remainingBytes <= 0)
            return null;

        var remainingSeconds = remainingBytes / averageBytesPerSecond;
        return TimeSpan.FromSeconds(remainingSeconds);
    }

    /// <summary>
    /// Calculates the estimated time for processing a single file based on its size and average processing speed.
    /// </summary>
    /// <param name="filePath">Path of the file to estimate.</param>
    /// <returns>Estimated time for the file, or null if no estimate can be calculated.</returns>
    private TimeSpan? CalculateFileEta(string filePath)
    {
        if (_fileProcessingStats.Count == 0)
            return null;

        double averageBytesPerSecond = _fileProcessingStats.Average(s => s.BytesPerSecond);
        if (averageBytesPerSecond <= 0)
            return null;

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return null;

            var estimatedSeconds = fileInfo.Length / averageBytesPerSecond;
            return TimeSpan.FromSeconds(estimatedSeconds);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Formats a timespan into a human-readable string.
    /// </summary>
    private static string FormatTimespan(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{time.Hours}h {time.Minutes}m {time.Seconds}s";
        if (time.TotalMinutes >= 1)
            return $"{time.Minutes}m {time.Seconds}s";
        return $"{time.Seconds}s";
    }

    /// <summary>
    /// Records file processing statistics for ETA calculation.
    /// </summary>
    private void RecordFileProcessingStats(string filePath)
    {
        if (_fileProcessingStopwatch == null)
            return;

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists)
            {
                var stats = new FileProcessingStats
                {
                    FileSizeBytes = fileInfo.Length,
                    ProcessingTime = _fileProcessingStopwatch.Elapsed
                };
                _fileProcessingStats.Add(stats);
                Logger.LogDebug("Recorded processing stats - Size: {FileSizeBytes} bytes, Time: {ProcessingTime}ms, Rate: {BytesPerSecond} bytes/sec",
                    stats.FileSizeBytes, _fileProcessingStopwatch.ElapsedMilliseconds, stats.BytesPerSecond);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to record file processing statistics");
        }
    }

    private void UpdateFileProgress(
        string status,
        string? currentOperation = null,
        int? percentComplete = null,
        ProgressRecordType recordType = ProgressRecordType.Processing,
        TimeSpan? eta = null)
    {
        var progressRecord = MediaConversionHelper.CreateNestedProgressRecord(
            FileProgressId,
            "File Conversion",
            status,
            BatchProgressId,
            currentOperation,
            percentComplete,
            recordType);

        if (eta.HasValue)
            progressRecord.StatusDescription += $" (File ETA: {FormatTimespan(eta.Value)})";

        WriteProgress(progressRecord);
    }

    private void ProcessFile(string inputPath)
    {
        _fileProcessingStopwatch = Stopwatch.StartNew();
        var fileName = Path.GetFileName(inputPath);
        UpdateFileProgress($"Preparing to convert {fileName}", fileName, percentComplete: 0);
        Logger.LogInformation("Processing file: {InputPath}", inputPath);

        // Resolve input path
        UpdateFileProgress("Resolving input path", fileName);
        if (!PathResolver.TryResolveInputPath(inputPath, out var resolvedInputPath))
        {
            _fileProcessingStopwatch.Stop();
            var result = new ConversionResult(inputPath, false, "File not found");
            _conversionResults.Add(result);
            UpdateFileProgress("Input file not found", fileName, recordType: ProgressRecordType.Completed);
            WriteError(new ErrorRecord(
                new FileNotFoundException($"Input media file not found: {inputPath}"),
                "FileNotFound",
                ErrorCategory.ObjectNotFound,
                inputPath));
            return;
        }

        // Resolve output path
        UpdateFileProgress("Resolving output path", fileName);
        var outputFileName = Path.GetFileNameWithoutExtension(resolvedInputPath) + ".mp4";
        var outputPath = Path.Combine(OutputDirectory, outputFileName);
        if (!PathResolver.TryResolveOutputPath(outputPath, out var resolvedOutputPath))
        {
            _fileProcessingStopwatch.Stop();
            var result = new ConversionResult(inputPath, false, "Failed to resolve output path");
            _conversionResults.Add(result);
            UpdateFileProgress("Failed to resolve output path", fileName, recordType: ProgressRecordType.Completed);
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
            UpdateFileProgress("Reading media metadata", fileName);
            mediaFile = MediaReaderService.GetMediaFileAsync(resolvedInputPath, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _fileProcessingStopwatch.Stop();
            Logger.LogError(ex, "Failed to read media file: {InputPath}", resolvedInputPath);
            var result = new ConversionResult(inputPath, false, $"Failed to read media file: {ex.Message}");
            _conversionResults.Add(result);
            UpdateFileProgress("Failed to read media metadata", fileName, recordType: ProgressRecordType.Completed);
            WriteError(new ErrorRecord(ex, "MediaReadFailed", ErrorCategory.ReadError, resolvedInputPath));
            return;
        }

        if (mediaFile == null)
        {
            _fileProcessingStopwatch.Stop();
            var result = new ConversionResult(inputPath, false, "Failed to read media file information");
            _conversionResults.Add(result);
            UpdateFileProgress("Failed to read media metadata", fileName, recordType: ProgressRecordType.Completed);
            WriteWarning($"Could not read media file information for: {inputPath}");
            return;
        }

        // Calculate ETA for this file (only available if we have prior processing stats)
        _currentFileEstimatedTime = CalculateFileEta(resolvedInputPath);

        // Determine audio track mappings
        AudioTrackMapping[] audioMappings;

        // If AudioTrackMappings is provided and not empty, use it for all files
        if (AudioTrackMappings != null && AudioTrackMappings.Length > 0)
        {
            UpdateFileProgress("Using provided audio mappings", fileName);
            audioMappings = AudioTrackMappings;
            Logger.LogInformation("Using provided audio track mappings for: {InputPath}", resolvedInputPath);
            UpdateFileProgress("Audio mappings ready", fileName, percentComplete: 40);
        }
        else
        {
            UpdateFileProgress("Detecting audio mappings", fileName);
            // Auto-detect and create mappings
            // Check for audio streams
            var audioStreams = mediaFile.Streams
                .Where(s => string.Equals(s.Type, "audio", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // If no audio streams at all, process with empty mappings (video-only)
            if (audioStreams.Count == 0)
            {
                Logger.LogInformation("No audio streams found in: {InputPath}, processing as video-only", resolvedInputPath);
                UpdateFileProgress("Starting conversion", Path.GetFileName(resolvedOutputPath), percentComplete: 50, eta: _currentFileEstimatedTime);
                if (ProcessConversion(resolvedInputPath, resolvedOutputPath, Array.Empty<AudioTrackMapping>(), inputPath))
                {
                    _fileProcessingStopwatch.Stop();
                    RecordFileProcessingStats(resolvedInputPath);
                    UpdateFileProgress("Conversion completed", fileName, recordType: ProgressRecordType.Completed);
                }
                return;
            }

            // Filter for English streams only
            var englishAudioStreams = audioStreams
                .Where(s => string.Equals(s.Language, "eng", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // If no English streams but other audio streams exist, use all audio streams
            List<MediaStream> streamsToUse;
            if (englishAudioStreams.Count == 0)
            {
                Logger.LogInformation("No English audio streams found in: {InputPath}, using all audio streams", resolvedInputPath);
                streamsToUse = audioStreams;
            }
            else
            {
                streamsToUse = englishAudioStreams;
            }

            try
            {
                audioMappings = CreateAudioTrackMappings(streamsToUse);
                UpdateFileProgress("Audio mappings ready", fileName, percentComplete: 40);
            }
            catch (Exception ex)
            {
                _fileProcessingStopwatch.Stop();
                Logger.LogError(ex, "Failed to create audio track mappings for: {InputPath}", resolvedInputPath);
                var result = new ConversionResult(inputPath, false, $"Auto-detection failed: {ex.Message}");
                _conversionResults.Add(result);
                UpdateFileProgress("Failed to detect audio mappings", fileName, recordType: ProgressRecordType.Completed);
                WriteWarning($"Audio settings can't be auto-detected for: {inputPath}. It must be processed manually. Error: {ex.Message}");
                return;
            }
        }

        // Perform conversion
        UpdateFileProgress("Starting conversion", Path.GetFileName(resolvedOutputPath), percentComplete: 50, eta: _currentFileEstimatedTime);
        if (ProcessConversion(resolvedInputPath, resolvedOutputPath, audioMappings, inputPath))
        {
            _fileProcessingStopwatch.Stop();
            RecordFileProcessingStats(resolvedInputPath);
            UpdateFileProgress("Conversion completed", fileName, recordType: ProgressRecordType.Completed);
        }
        else
        {
            _fileProcessingStopwatch.Stop();
        }
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

    private bool ProcessConversion(string resolvedInputPath, string resolvedOutputPath, AudioTrackMapping[] audioMappings, string originalInputPath)
    {
        try
        {
            // Get or create video encoding settings
            var videoSettings = VideoEncodingSettings ?? CreateDefaultVideoEncodingSettings();
            var additionalArguments = MediaConversionHelper.BuildX265Arguments(X265Params, videoSettings.Codec);

            Logger.LogDebug("Starting media file conversion: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var outputFileName = Path.GetFileName(resolvedOutputPath);
            UpdateFileProgress(
                $"Encoding to {videoSettings.Codec} ({videoSettings.Preset} preset)",
                outputFileName,
                percentComplete: 60);

            var spinner = new[] { "|", "/", "-", "\\" };
            var spinnerIndex = 0;
            var conversionTask = Task.Run(() => MediaConversionService.ExecuteConversion(
                resolvedInputPath,
                resolvedOutputPath,
                videoSettings,
                audioMappings,
                additionalArguments));

            while (!conversionTask.Wait(TimeSpan.FromSeconds(0.1)))
            {
                var indicator = spinner[spinnerIndex];
                spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                UpdateFileProgress($"Encoding {indicator}", outputFileName, percentComplete: 60);
            }

            conversionTask.GetAwaiter().GetResult();

            Logger.LogInformation("Successfully converted media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var result = new ConversionResult(originalInputPath, true, "Success");
            _conversionResults.Add(result);
            return true;
        }
        catch (FfmpegConversionException ex)
        {
            Logger.LogError(ex, "FFmpeg conversion failed: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var statusMessage = BuildStatusMessage(ex);
            var result = new ConversionResult(originalInputPath, false, statusMessage);
            _conversionResults.Add(result);
            UpdateFileProgress(statusMessage, Path.GetFileName(resolvedInputPath), recordType: ProgressRecordType.Completed);
            WriteError(new ErrorRecord(ex, "ConversionFailed", ErrorCategory.OperationStopped, resolvedInputPath));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while converting media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var result = new ConversionResult(originalInputPath, false, $"Conversion failed: {ex.Message}");
            _conversionResults.Add(result);
            UpdateFileProgress("Conversion failed", Path.GetFileName(resolvedInputPath), recordType: ProgressRecordType.Completed);
            WriteError(new ErrorRecord(ex, "ConversionFailed", ErrorCategory.OperationStopped, resolvedInputPath));
        }

        return false;
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
