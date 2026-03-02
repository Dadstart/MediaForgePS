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
/// based on codec type and channel count. It applies default video encoding settings based on DefaultVideoEncoder
/// (x264/x265: libx264/libx265, CRF 18, preset medium; nvenc: hevc_nvenc, CQ 18, preset p5) unless overridden via VideoEncodingSettings.
/// Audio track mappings can be provided via the AudioTrackMappings parameter; if not provided, they are
/// automatically detected and created for each file.
/// </remarks>
[Cmdlet(VerbsData.Convert, "MediaFiles", DefaultParameterSetName = DefaultEncoderParameterSet)]
[OutputType(typeof(ConversionResult))]
public class ConvertMediaFilesCommand : CmdletBase
{
    private const string DefaultEncoderParameterSet = "DefaultEncoder";
    private const string ExplicitSettingsParameterSet = "ExplicitSettings";
    private static class HelpMessages
    {
        public const string InputPath = "Array of input file paths to convert";
        public const string OutputDirectory = "Directory where output files will be written (files keep original name with .mkv extension)";
        public const string VideoEncodingSettings = "Override default video encoding settings. If not provided, uses default for DefaultVideoEncoder";
        public const string DefaultVideoEncoder = "Default encoder to use when VideoEncodingSettings is not specified: 'x264' (libx264), 'x265' (libx265), or 'nvenc' (NVENC HEVC)";
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
        ParameterSetName = DefaultEncoderParameterSet,
        HelpMessage = HelpMessages.InputPath)]
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = ExplicitSettingsParameterSet,
        HelpMessage = HelpMessages.InputPath)]
    [ValidateNotNullOrEmpty]
    public object[] InputPath { get; set; } = Array.Empty<object>();

    /// <summary>
    /// Directory where output files will be written. Files keep original name with .mkv extension.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = DefaultEncoderParameterSet,
        HelpMessage = HelpMessages.OutputDirectory)]
    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = ExplicitSettingsParameterSet,
        HelpMessage = HelpMessages.OutputDirectory)]
    [ValidateNotNullOrEmpty]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Video encoding settings to use. Mutually exclusive with DefaultVideoEncoder.
    /// </summary>
    [Parameter(
        Mandatory = true,
        ParameterSetName = ExplicitSettingsParameterSet,
        HelpMessage = HelpMessages.VideoEncodingSettings)]
    public VideoEncodingSettings? VideoEncodingSettings { get; set; }

    /// <summary>
    /// Default encoder to use: 'x264' (libx264), 'x265' (libx265), or 'nvenc' (NVENC HEVC). Mutually exclusive with VideoEncodingSettings.
    /// </summary>
    [Parameter(
        ParameterSetName = DefaultEncoderParameterSet,
        HelpMessage = HelpMessages.DefaultVideoEncoder)]
    [ValidateSet("x264", "x265", "nvenc", IgnoreCase = true)]
    public string? DefaultVideoEncoder { get; set; }

    /// <summary>
    /// Audio track mappings to use for all files. If not provided, mappings are automatically detected and created for each file.
    /// </summary>
    [Parameter(
        Mandatory = false,
        ParameterSetName = DefaultEncoderParameterSet,
        HelpMessage = HelpMessages.AudioTrackMappings)]
    [Parameter(
        Mandatory = false,
        ParameterSetName = ExplicitSettingsParameterSet,
        HelpMessage = HelpMessages.AudioTrackMappings)]
    public AudioTrackMapping[] AudioTrackMappings { get; set; } = Array.Empty<AudioTrackMapping>();

    /// <summary>
    /// Additional x265 params to pass through to ffmpeg.
    /// </summary>
    [Parameter(
        Mandatory = false,
        ParameterSetName = DefaultEncoderParameterSet,
        HelpMessage = HelpMessages.X265Params)]
    [Parameter(
        Mandatory = false,
        ParameterSetName = ExplicitSettingsParameterSet,
        HelpMessage = HelpMessages.X265Params)]
    public string? X265Params { get; set; }

    private IPathResolver? _pathResolver;
    private IMediaConversionService? _mediaConversionService;
    private IMediaReaderService? _mediaReaderService;
    private IAudioTrackMappingService? _audioTrackMappingService;
    private readonly List<ConversionResult> _conversionResults = new();
    private readonly HashSet<string> _uniqueInputPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileProcessingStats> _fileProcessingStats = new();
    private int _currentFileIndex = 0;
    private Stopwatch? _fileProcessingStopwatch;
    private TimeSpan? _currentFileEstimatedTime;
    private Stopwatch? _batchStopwatch;
    private int _batchTotalFiles = 0;
    private long _batchTotalBytes = 0;
    private long _batchCompletedBytes = 0;
    private List<(string Path, long Size)>? _inputPathsWithSize;

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
    /// Audio track mapping service for automatic mapping generation.
    /// </summary>
    private IAudioTrackMappingService AudioTrackMappingService => _audioTrackMappingService ??= ModuleServices.GetRequiredService<IAudioTrackMappingService>();

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
            var sizedPaths = MediaConversionHelper.BuildItemsWithSizes(_uniqueInputPaths, static path => path, out _batchTotalBytes);
            _inputPathsWithSize = sizedPaths
                .Select(entry => (Path: entry.Item, entry.Size))
                .ToList();

            var totalFiles = _inputPathsWithSize.Count;
            _currentFileIndex = 0;
            _batchTotalFiles = totalFiles;
            _batchCompletedBytes = 0;
            _batchStopwatch = Stopwatch.StartNew();

            WriteHostMessage($"Converting {totalFiles} file(s) (total size: {MediaConversionHelper.FormatByteCount(_batchTotalBytes)})", ConsoleColor.Cyan);
            WriteHostMessage($"  Output: {OutputDirectory}", ConsoleColor.Gray);

            foreach (var (inputPath, fileSize) in _inputPathsWithSize)
            {
                _currentFileIndex++;
                var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                    _currentFileIndex, totalFiles, Path.GetFileName(inputPath), _batchCompletedBytes, _batchTotalBytes);
                var batchEta = CalculateRemainingTime(inputPath, totalFiles - _currentFileIndex);
                MediaConversionHelper.WriteMainProgress(this, "Batch Conversion", status, percent, batchEta, ProgressRecordType.Processing);
                ProcessFile(inputPath);
                if (_conversionResults.Count > 0 && _conversionResults[^1].Success)
                    _batchCompletedBytes += fileSize;
            }

            _batchStopwatch.Stop();

            MediaConversionHelper.WriteProgressCompleted(this, "Batch Conversion", "File Conversion");

            WriteHostMessage("Batch conversion completed", ConsoleColor.Green);
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
    /// Calculates the estimated remaining time based on file sizes and average processing speed.
    /// </summary>
    /// <param name="currentFilePath">Path of the current file being processed.</param>
    /// <param name="remainingFilesCount">Number of remaining files after the current one.</param>
    /// <returns>Estimated time remaining, or null if no estimate can be calculated.</returns>
    private TimeSpan? CalculateRemainingTime(string currentFilePath, int remainingFilesCount)
    {
        long remainingBytes = 0;
        try
        {
            var currentFile = new FileInfo(currentFilePath);
            if (currentFile.Exists)
                remainingBytes = currentFile.Length;
        }
        catch
        {
            return null;
        }

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
                // Continue with what we have
            }
        }

        return MediaConversionHelper.CalculateRemainingTime(
            remainingBytes,
            _fileProcessingStats.Select(s => (s.FileSizeBytes, s.ProcessingTime)));
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

    /// <summary>
    /// Handles file processing errors with consistent logging, result recording, and progress updates.
    /// </summary>
    private void HandleFileError(string inputPath, string fileName, string errorMessage, Exception? exception = null, ErrorCategory errorCategory = ErrorCategory.NotSpecified)
    {
        _fileProcessingStopwatch?.Stop();
        var result = new ConversionResult(inputPath, false, errorMessage);
        _conversionResults.Add(result);
        UpdateFileProgress(errorMessage, fileName, recordType: ProgressRecordType.Completed);

        if (exception != null)
            WriteError(new ErrorRecord(exception, "ProcessingFailed", errorCategory, inputPath));
    }

    private void UpdateFileProgress(
        string status,
        string? currentOperation = null,
        int? percentComplete = null,
        ProgressRecordType recordType = ProgressRecordType.Processing,
        TimeSpan? eta = null) =>
        MediaConversionHelper.WriteCurrentItemProgress(this, "File Conversion", status, currentOperation, percentComplete, eta, recordType);

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
            HandleFileError(inputPath, fileName, "File not found",
                new FileNotFoundException($"Input media file not found: {inputPath}"),
                ErrorCategory.ObjectNotFound);
            return;
        }

        // Resolve output path
        UpdateFileProgress("Resolving output path", fileName);
        var outputFileName = Path.GetFileNameWithoutExtension(resolvedInputPath) + ".mp4";
        var outputPath = Path.Combine(OutputDirectory, outputFileName);
        if (!PathResolver.TryResolveOutputPath(outputPath, out var resolvedOutputPath))
        {
            HandleFileError(inputPath, fileName, "Failed to resolve output path",
                new InvalidOperationException($"Failed to resolve output path: {outputPath}"),
                ErrorCategory.InvalidArgument);
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
            Logger.LogError(ex, "Failed to read media file: {InputPath}", resolvedInputPath);
            HandleFileError(inputPath, fileName, $"Failed to read media file: {ex.Message}", ex, ErrorCategory.ReadError);
            return;
        }

        if (mediaFile == null)
        {
            HandleFileError(inputPath, fileName, "Failed to read media file information");
            WriteWarning($"Could not read media file information for: {inputPath}");
            return;
        }

        // Calculate ETA for this file (only available if we have prior processing stats)
        _currentFileEstimatedTime = CalculateFileEta(resolvedInputPath);

        if (!TryResolveAudioMappings(inputPath, fileName, resolvedInputPath, mediaFile, out var audioMappings))
            return;

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

    private bool TryResolveAudioMappings(string inputPath, string fileName, string resolvedInputPath, MediaFile mediaFile, out AudioTrackMapping[] audioMappings)
    {
        audioMappings = Array.Empty<AudioTrackMapping>();

        if (AudioTrackMappings is { Length: > 0 })
        {
            UpdateFileProgress("Using provided audio mappings", fileName);
            audioMappings = AudioTrackMappings;
            Logger.LogInformation("Using provided audio track mappings for: {InputPath}", resolvedInputPath);
            UpdateFileProgress("Audio mappings ready", fileName, percentComplete: 40);
            return true;
        }

        UpdateFileProgress("Detecting audio mappings", fileName);
        var audioSelection = MediaConversionHelper.SelectPreferredAudioStreams(mediaFile.Streams);
        if (audioSelection.TotalAudioStreamCount == 0)
        {
            Logger.LogInformation("No audio streams found in: {InputPath}, processing as video-only", resolvedInputPath);
            return true;
        }

        if (audioSelection.EnglishAudioStreamCount == 0)
            Logger.LogInformation("No English audio streams found in: {InputPath}, using all audio streams", resolvedInputPath);

        try
        {
            audioMappings = AudioTrackMappingService.CreateAutomaticMappings(audioSelection.SelectedStreams);
            UpdateFileProgress("Audio mappings ready", fileName, percentComplete: 40);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create audio track mappings for: {InputPath}", resolvedInputPath);
            HandleFileError(inputPath, fileName, $"Auto-detection failed: {ex.Message}", ex);
            WriteWarning($"Audio settings can't be auto-detected for: {inputPath}. It must be processed manually. Error: {ex.Message}");
            return false;
        }
    }

    private bool ProcessConversion(string resolvedInputPath, string resolvedOutputPath, AudioTrackMapping[] audioMappings, string originalInputPath)
    {
        try
        {
            // Get or create video encoding settings
            var videoSettings = VideoEncodingSettings ?? MediaConversionHelper.CreateDefaultVideoEncodingSettings(DefaultVideoEncoder);
            var additionalArguments = MediaConversionHelper.BuildX265Arguments(X265Params, videoSettings.Codec);

            Logger.LogDebug("Starting media file conversion: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            var outputFileName = Path.GetFileName(resolvedOutputPath);
            UpdateFileProgress(
                $"Encoding to {videoSettings.Codec} ({videoSettings.Preset} preset)",
                outputFileName,
                percentComplete: 60);

            var spinner = new[] { "|", "/", "-", "\\" };
            var spinnerIndex = 0;
            var lastBatchUpdateTime = DateTime.UtcNow;
            var conversionTask = Task.Run(() => MediaConversionService.ExecuteConversion(
                resolvedInputPath,
                resolvedOutputPath,
                videoSettings,
                audioMappings,
                additionalArguments));

            // Calculate initial batch ETA
            TimeSpan? initialBatchEta = null;
            if (_currentFileIndex < _batchTotalFiles)
            {
                var remainingFiles = _batchTotalFiles - _currentFileIndex;
                initialBatchEta = CalculateRemainingTime(resolvedInputPath, remainingFiles);
            }

            while (!conversionTask.Wait(TimeSpan.FromSeconds(0.05)))
            {
                var indicator = spinner[spinnerIndex];
                spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                UpdateFileProgress($"{outputFileName} {indicator}", outputFileName, percentComplete: 60);

                // Update batch progress with countdown every second
                var now = DateTime.UtcNow;
                if ((now - lastBatchUpdateTime).TotalSeconds >= 1.0 && initialBatchEta.HasValue && _batchStopwatch != null)
                {
                    var remaining = initialBatchEta.Value - _batchStopwatch.Elapsed;
                    if (remaining.TotalSeconds > 0)
                    {
                        var (batchStatus, batchPercent) = MediaConversionHelper.BuildBatchProgressStatus(
                            _currentFileIndex, _batchTotalFiles, Path.GetFileName(resolvedInputPath), _batchCompletedBytes, _batchTotalBytes);
                        MediaConversionHelper.WriteMainProgress(this, "Batch Conversion", batchStatus, batchPercent, remaining, ProgressRecordType.Processing);
                    }
                    lastBatchUpdateTime = now;
                }
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
            var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
            HandleFileError(originalInputPath, Path.GetFileName(resolvedInputPath), statusMessage, ex, ErrorCategory.OperationStopped);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while converting media file: {ResolvedInputPath} -> {ResolvedOutputPath}", resolvedInputPath, resolvedOutputPath);
            HandleFileError(originalInputPath, Path.GetFileName(resolvedInputPath), $"Conversion failed: {ex.Message}", ex, ErrorCategory.OperationStopped);
        }

        return false;
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
