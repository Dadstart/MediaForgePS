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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts video files in a directory (or specified paths) to MP4 using module conversion services.
/// Supports common container extensions: .mkv, .mp4, .m4v, .mov, .avi, .wmv, .flv, .webm, .mpg, .mpeg,
/// .ts, .m2ts, .mts, .vob, .ogv, .3gp, .asf.
/// </summary>
[Cmdlet(VerbsData.Convert, "VideoFile")]
[OutputType(typeof(VideoFileConversionResult))]
public class ConvertVideoFileCommand : CmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    private IPathResolver? _pathResolver;
    private IMediaReaderService? _mediaReaderService;
    private IAudioTrackMappingService? _audioTrackMappingService;
    private IMediaConversionService? _mediaConversionService;
    private readonly List<VideoFileConversionResult> _results = new();
    private readonly List<(long FileSizeBytes, TimeSpan ProcessingTime)> _completedFileStats = new();
    private List<(string Path, string InputRoot, string OutputRoot, long Size)>? _sizedVideoFiles;
    private Stopwatch? _batchStopwatch;
    private Stopwatch? _fileStopwatch;
    private int _currentFileIndex;
    private int _batchTotalFiles;
    private long _batchTotalBytes;
    private long _batchCompletedBytes;
    private TimeSpan? _currentFileEstimatedTime;

    /// <summary>
    /// Directory containing video files, a single video file path, or an array of video file paths.
    /// Supported extensions: .mkv, .mp4, .m4v, .mov, .avi, .wmv, .flv, .webm, .mpg, .mpeg, .ts, .m2ts,
    /// .mts, .vob, .ogv, .3gp, .asf.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Directory containing video files, a single video file path, or an array of video file paths.")]
    [Alias("InputDirectory", "Path")]
    [ValidateNotNullOrEmpty]
    public string[] InputPath { get; set; } = [];

    /// <summary>
    /// Directory where converted files are written. Defaults to InputPath.
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 1,
        HelpMessage = "Directory where converted files are written. Defaults to InputPath.")]
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Includes video files in child directories.
    /// </summary>
    [Parameter(HelpMessage = "Include video files in subdirectories.")]
    public SwitchParameter Recurse { get; set; }

    /// <summary>
    /// Default encoder to use: x264, x265, or nvenc.
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Default encoder to use: 'x264', 'x265', or 'nvenc'")]
    [ValidateSet("x264", "x265", "nvenc", IgnoreCase = true)]
    public string DefaultVideoEncoder { get; set; } = "nvenc";

    /// <summary>
    /// Additional x265 params passed to Ffmpeg with -x265-params.
    /// </summary>
    [Parameter(Mandatory = false, HelpMessage = "Additional x265 params (passed to ffmpeg via -x265-params).")]
    public string? X265Params { get; set; }

    /// <summary>
    /// When specified, skips caption extraction after file conversion.
    /// </summary>
    [Parameter(HelpMessage = "Skip caption extraction after converting video files.")]
    public SwitchParameter SkipSubtitles { get; set; }

    /// <summary>
    /// When specified, converts image-based captions (SUP, SUB) to SRT via OCR and repairs SRT files unless -SkipRepair is specified. Has no effect when -SkipSubtitles is specified.
    /// </summary>
    [Parameter(HelpMessage = "Convert image captions to SRT via OCR and repair SRT files.")]
    public SwitchParameter Ocr { get; set; }

    /// <summary>
    /// When specified, skips the SRT repair step during OCR processing. Has no effect when -Ocr is not specified.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair during OCR processing.")]
    public SwitchParameter SkipRepair { get; set; }

    private const int DefaultOcrThrottleLimit = 10;

    private static readonly HashSet<string> _supportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".flv",
        ".webm", ".mpg", ".mpeg", ".ts", ".m2ts", ".mts", ".vob",
        ".ogv", ".3gp", ".asf",
    };

    private IExecutableService? _executableService;

    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    private IAudioTrackMappingService AudioTrackMappingService => _audioTrackMappingService ??= ModuleServices.GetRequiredService<IAudioTrackMappingService>();

    private IMediaConversionService MediaConversionService => _mediaConversionService ??= ModuleServices.GetRequiredService<IMediaConversionService>();

    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();

    protected override void Process()
    {
        _results.Clear();
        _completedFileStats.Clear();
        _sizedVideoFiles = null;
        _batchCompletedBytes = 0;
        _currentFileIndex = 0;

        var resolvedEntries = global::Dadstart.Labs.MediaForge.Services.System.PathResolver.ResolveFileOrDirectoryPaths(
            this,
            InputPath,
            Logger,
            WriteError);
        if (resolvedEntries.Count == 0)
            return;

        string? configuredOutputDirectory = null;
        if (!string.IsNullOrWhiteSpace(OutputDirectory) && !TryResolveDirectoryPath(OutputDirectory!, requireExists: false, out configuredOutputDirectory))
        {
            WriteError(CreateErrorRecord(
                new InvalidOperationException($"Failed to resolve output directory: {OutputDirectory}"),
                ErrorIds.OutputPathResolutionFailed,
                ErrorCategory.InvalidArgument,
                OutputDirectory));
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuredOutputDirectory))
            Directory.CreateDirectory(configuredOutputDirectory);

        var videoFileInputs = new List<(string Path, string InputRoot, string OutputRoot)>();
        var seenVideoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (resolvedInputPath, isDirectory) in resolvedEntries)
        {
            if (isDirectory)
            {
                var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var directoryFiles = Directory.EnumerateFiles(resolvedInputPath, "*.*", searchOption)
                    .Where(path => _supportedVideoExtensions.Contains(Path.GetExtension(path)))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
                foreach (var directoryFile in directoryFiles)
                {
                    if (seenVideoPaths.Add(directoryFile))
                    {
                        var outputRoot = configuredOutputDirectory ?? resolvedInputPath;
                        videoFileInputs.Add((directoryFile, resolvedInputPath, outputRoot));
                    }
                }
            }
            else
            {
                if (!_supportedVideoExtensions.Contains(Path.GetExtension(resolvedInputPath)))
                {
                    var supportedList = string.Join(", ", _supportedVideoExtensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase));
                    WriteError(CreateErrorRecord(
                        new ArgumentException($"Input file extension is not a supported video format: {resolvedInputPath}. Supported extensions: {supportedList}"),
                        "InvalidInputPath",
                        ErrorCategory.InvalidArgument,
                        resolvedInputPath));
                    return;
                }

                var inputRoot = Path.GetDirectoryName(resolvedInputPath) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(inputRoot))
                {
                    WriteError(CreateErrorRecord(
                        new InvalidOperationException($"Could not determine containing directory for file: {resolvedInputPath}"),
                        ErrorIds.OutputPathResolutionFailed,
                        ErrorCategory.InvalidArgument,
                        resolvedInputPath));
                    return;
                }

                if (seenVideoPaths.Add(resolvedInputPath))
                {
                    var outputRoot = configuredOutputDirectory ?? inputRoot;
                    videoFileInputs.Add((resolvedInputPath, inputRoot, outputRoot));
                }
            }
        }

        if (videoFileInputs.Count == 0)
        {
            WriteWarning("No supported video files found in the specified input paths.");
            return;
        }

        var sized = MediaConversionHelper.BuildItemsWithSizes(videoFileInputs, static item => item.Path, out var totalBytes);
        _sizedVideoFiles = sized.Select(entry => (entry.Item.Path, entry.Item.InputRoot, entry.Item.OutputRoot, entry.Size)).ToList();
        _batchTotalBytes = totalBytes;
        _batchTotalFiles = _sizedVideoFiles.Count;
        _batchStopwatch = Stopwatch.StartNew();

        WriteHostMessage(
            $"Converting {_batchTotalFiles} video file(s) (total size: {MediaConversionHelper.FormatByteCount(_batchTotalBytes)})",
            ConsoleColor.Cyan);
        WriteHostMessage(
            string.IsNullOrWhiteSpace(configuredOutputDirectory)
                ? "  Output: source directories"
                : $"  Output: {configuredOutputDirectory}",
            ConsoleColor.Gray);

        var videoSettings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(DefaultVideoEncoder);
        var additionalArguments = MediaConversionHelper.BuildX265Arguments(X265Params, videoSettings.Codec);

        foreach (var (inputPath, inputRoot, outputRoot, fileSize) in _sizedVideoFiles)
        {
            _currentFileIndex++;
            var fileName = GetFileName(inputPath);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                _currentFileIndex, _batchTotalFiles, fileName, _batchCompletedBytes, _batchTotalBytes);
            var batchEta = CalculateBatchRemainingTime(inputPath, _batchTotalFiles - _currentFileIndex);
            MediaConversionHelper.WriteMainProgress(
                this, "Video file conversion", status, percent, batchEta, ProgressRecordType.Processing);

            var result = ConvertSingleFile(
                inputRoot,
                outputRoot,
                inputPath,
                fileName,
                videoSettings,
                additionalArguments);

            _results.Add(result);
            WriteObject(result);

            if (result.Success)
                _batchCompletedBytes += fileSize;
        }

        _batchStopwatch?.Stop();
        MediaConversionHelper.WriteProgressCompleted(this, "Video file conversion", "File conversion");

        if (!SkipSubtitles.IsPresent)
        {
            var successes = _results.Where(r => r.Success).ToList();
            var extractedCaptionPaths = new List<string>();
            if (successes.Count > 0)
            {
                WriteHostMessage(string.Empty);
                WriteHostMessage("Extracting captions...", ConsoleColor.Cyan);
                var total = successes.Count;
                for (var i = 0; i < successes.Count; i++)
                {
                    var r = successes[i];
                    var current = i + 1;
                    var name = GetFileName(r.InputPath);
                    var (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, name);
                    MediaConversionHelper.WriteMainProgress(this, "Caption extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
                    MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Extracting captions...", name, recordType: ProgressRecordType.Processing);

                    extractedCaptionPaths.AddRange(ExtractEnglishSubtitlesToOutputSidecars(r.InputPath, r.OutputPath));

                    (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, name);
                    MediaConversionHelper.WriteMainProgress(this, "Caption extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
                    MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Completed", name, recordType: ProgressRecordType.Completed);
                }

                MediaConversionHelper.WriteProgressCompleted(this, "Caption extraction", "Current file");
                WriteHostMessage(
                    $"  Processed {total} file(s), {extractedCaptionPaths.Count} caption file(s) extracted.",
                    ConsoleColor.Green);
                WriteVerbose($"Caption extraction - files: {total}, paths: {extractedCaptionPaths.Count}.");

                if (Ocr.IsPresent)
                {
                    if (extractedCaptionPaths.Count > 0)
                    {
                        var imagePaths = SubtitlePathHelper.GetImageSubtitlePaths(extractedCaptionPaths);
                        var srtPathsFromCaptions = SubtitlePathHelper.GetSrtPaths(extractedCaptionPaths);

                        if (imagePaths.Count > 0 || srtPathsFromCaptions.Count > 0)
                        {
                            WriteHostMessage("  Running OCR and repair on extracted captions...", ConsoleColor.Cyan);

                            var allSrtPaths = SubtitleOcrRepairWorkflow.Run(
                                this,
                                Logger,
                                ExecutableService,
                                PathResolver,
                                imagePaths,
                                srtPathsFromCaptions,
                                performOcr: true,
                                DefaultOcrThrottleLimit,
                                shouldRepair: !SkipRepair.IsPresent,
                                backupPath: null);

                            if (allSrtPaths == null)
                                return;

                            if (allSrtPaths.Count == 0)
                                WriteHostMessage("  No SRT files to repair (only non-SRT formats were extracted).", ConsoleColor.Green);
                            else
                                WriteHostMessage("  Caption OCR and repair completed.", ConsoleColor.Green);
                        }
                    }
                }
            }
        }

        var ok = _results.Count(r => r.Success);
        var failed = _results.Count - ok;
        if (failed == 0)
            WriteHostMessage($"Directory conversion finished: {ok} file(s) OK.", ConsoleColor.Green);
        else
            WriteHostMessage($"Directory conversion finished: {ok} succeeded, {failed} failed.", ConsoleColor.Yellow);
    }

    private VideoFileConversionResult ConvertSingleFile(
        string resolvedInputDirectory,
        string resolvedOutputDirectory,
        string inputPath,
        string fileName,
        VideoEncodingSettings videoSettings,
        string[]? additionalArguments)
    {
        _fileStopwatch = Stopwatch.StartNew();
        UpdateFileProgress($"Preparing {fileName}", fileName, percentComplete: 0);

        try
        {
            UpdateFileProgress("Reading media metadata", fileName);
            if (!TryGetMediaFile(MediaReaderService, inputPath, out var mediaFile))
            {
                _fileStopwatch.Stop();
                UpdateFileProgress("Failed to read media metadata", fileName, recordType: ProgressRecordType.Completed);
                return new VideoFileConversionResult(inputPath, inputPath, false, "Failed to read media metadata.");
            }

            UpdateFileProgress("Building audio track mappings", fileName);
            var audioMappings = AudioTrackMappingService.CreateDirectoryEncodeMappings(mediaFile);
            var outputPath = BuildOutputPath(resolvedInputDirectory, resolvedOutputDirectory, inputPath);

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            UpdateFileProgress("Resolving output path", fileName);
            if (!PathResolver.TryResolveOutputPath(outputPath, out var resolvedOutputPath))
            {
                _fileStopwatch.Stop();
                UpdateFileProgress("Failed to resolve output path", fileName, recordType: ProgressRecordType.Completed);
                return new VideoFileConversionResult(
                    inputPath,
                    outputPath,
                    false,
                    "Failed to resolve output path.");
            }

            _currentFileEstimatedTime = CalculateFileEta(inputPath);
            var outName = GetFileName(resolvedOutputPath);
            UpdateFileProgress(
                $"Encoding to {videoSettings.Codec}",
                outName,
                percentComplete: 50,
                eta: _currentFileEstimatedTime);

            RunConversionWithProgress(
                inputPath,
                resolvedOutputPath,
                videoSettings,
                audioMappings,
                additionalArguments,
                outName);

            _fileStopwatch.Stop();
            RecordFileProcessingStats(inputPath);
            UpdateFileProgress("Conversion completed", fileName, recordType: ProgressRecordType.Completed);
            return new VideoFileConversionResult(inputPath, resolvedOutputPath, true, "Success");
        }
        catch (FfmpegConversionException ex)
        {
            _fileStopwatch?.Stop();
            var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
            UpdateFileProgress("Conversion failed", fileName, recordType: ProgressRecordType.Completed);
            return new VideoFileConversionResult(inputPath, inputPath, false, statusMessage);
        }
        catch (Exception ex)
        {
            _fileStopwatch?.Stop();
            UpdateFileProgress("Error", fileName, recordType: ProgressRecordType.Completed);
            return new VideoFileConversionResult(inputPath, inputPath, false, ex.Message);
        }
    }

    private void RunConversionWithProgress(
        string resolvedInputPath,
        string resolvedOutputPath,
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        string[]? additionalArguments,
        string outputFileName)
    {
        try
        {
            Logger.LogInformation("Starting conversion: {Input} -> {Output}", resolvedInputPath, resolvedOutputPath);
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

            TimeSpan? initialBatchEta = null;
            if (_currentFileIndex < _batchTotalFiles)
            {
                var remainingFiles = _batchTotalFiles - _currentFileIndex;
                initialBatchEta = CalculateBatchRemainingTime(resolvedInputPath, remainingFiles);
            }

            while (!conversionTask.Wait(TimeSpan.FromSeconds(0.05)))
            {
                var indicator = spinner[spinnerIndex];
                spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                UpdateFileProgress($"{outputFileName} {indicator}", outputFileName, percentComplete: 60);

                var now = DateTime.UtcNow;
                if ((now - lastBatchUpdateTime).TotalSeconds >= 1.0 && initialBatchEta.HasValue && _batchStopwatch != null)
                {
                    var remaining = initialBatchEta.Value - _batchStopwatch.Elapsed;
                    if (remaining.TotalSeconds > 0)
                    {
                        var fileName = GetFileName(resolvedInputPath);
                        var (batchStatus, batchPercent) = MediaConversionHelper.BuildBatchProgressStatus(
                            _currentFileIndex,
                            _batchTotalFiles,
                            fileName,
                            _batchCompletedBytes,
                            _batchTotalBytes);
                        MediaConversionHelper.WriteMainProgress(
                            this,
                            "Video file conversion",
                            batchStatus,
                            batchPercent,
                            remaining,
                            ProgressRecordType.Processing);
                    }

                    lastBatchUpdateTime = now;
                }
            }

            conversionTask.GetAwaiter().GetResult();
            Logger.LogInformation("Converted: {Input} -> {Output}", resolvedInputPath, resolvedOutputPath);
        }
        catch (FfmpegConversionException ex)
        {
            Logger.LogError(ex, "FFmpeg conversion failed: {Input} -> {Output}", resolvedInputPath, resolvedOutputPath);
            var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
            UpdateFileProgress(statusMessage, outputFileName, recordType: ProgressRecordType.Completed);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Conversion failed: {Input} -> {Output}", resolvedInputPath, resolvedOutputPath);
            UpdateFileProgress(ex.Message, outputFileName, recordType: ProgressRecordType.Completed);
            throw;
        }
    }

    private void UpdateFileProgress(
        string status,
        string? currentOperation = null,
        int? percentComplete = null,
        ProgressRecordType recordType = ProgressRecordType.Processing,
        TimeSpan? eta = null) =>
        MediaConversionHelper.WriteCurrentItemProgress(this, "File conversion", status, currentOperation, percentComplete, eta, recordType);

    private TimeSpan? CalculateBatchRemainingTime(string currentFilePath, int remainingFilesCount)
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

        if (_sizedVideoFiles == null)
            return null;

        var remainingPaths = _sizedVideoFiles.Skip(_currentFileIndex).Take(remainingFilesCount);
        foreach (var entry in remainingPaths)
        {
            try
            {
                remainingBytes += entry.Size;
            }
            catch
            {
                // Continue with partial data.
            }
        }

        return MediaConversionHelper.CalculateRemainingTime(remainingBytes, _completedFileStats);
    }

    private TimeSpan? CalculateFileEta(string filePath)
    {
        if (_completedFileStats.Count == 0)
            return null;

        var averageBytesPerSecond = _completedFileStats.Average(s =>
            s.FileSizeBytes > 0 && s.ProcessingTime.TotalSeconds > 0
                ? s.FileSizeBytes / s.ProcessingTime.TotalSeconds
                : 0);
        if (averageBytesPerSecond <= 0)
            return null;

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return null;

            return TimeSpan.FromSeconds(fileInfo.Length / averageBytesPerSecond);
        }
        catch
        {
            return null;
        }
    }

    private void RecordFileProcessingStats(string resolvedInputPath)
    {
        if (_fileStopwatch == null)
            return;

        try
        {
            var fileInfo = new FileInfo(resolvedInputPath);
            if (fileInfo.Exists)
            {
                _completedFileStats.Add((fileInfo.Length, _fileStopwatch.Elapsed));
                Logger.LogDebug(
                    "Recorded video file conversion stats: {Bytes} bytes in {Ms} ms",
                    fileInfo.Length,
                    _fileStopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to record file processing statistics");
        }
    }

    private static string GetFileName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var separatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return separatorIndex >= 0 ? path[(separatorIndex + 1)..] : path;
    }

    private static string BuildOutputPath(string inputRoot, string outputRoot, string inputPath)
    {
        var relativePath = Path.GetRelativePath(inputRoot, inputPath);
        var outputRelativePath = Path.ChangeExtension(relativePath, ".mp4");
        return Path.Combine(outputRoot, outputRelativePath);
    }

    private IReadOnlyList<string> ExtractEnglishSubtitlesToOutputSidecars(string sourceMkvPath, string resolvedOutputMp4Path)
    {
        MediaFile? mediaFile;
        try
        {
            mediaFile = MediaReaderService.GetMediaFileAsync(sourceMkvPath, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not read media file for caption extraction: {Path}", sourceMkvPath);
            return Array.Empty<string>();
        }

        if (mediaFile == null)
            return Array.Empty<string>();

        var mkvextractPath = WindowsExecutablePathHelper.GetMkvextractPath();
        var fileName = Path.GetFileName(sourceMkvPath);

        return SubtitleExportHelper.ExtractEnglishSubtitles(
            ExecutableService,
            mediaFile,
            mkvextractPath,
            buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                resolvedOutputMp4Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
            finalizeOutputPath: candidate =>
            {
                if (PathResolver.TryResolveOutputPath(candidate, out var resolved))
                    return resolved;
                Logger.LogWarning("Failed to resolve caption output path: {Path}", candidate);
                return null;
            },
            onUnknownCodec: stream => WriteWarning($"Unknown codec: {stream.Codec} - using .bin extension"),
            onExtractFailed: (_, ex) => WriteStandardError(ex, ErrorIds.SubtitleExportFailed, ErrorCategory.OperationStopped, sourceMkvPath),
            onNoEnglishSubtitles: () => WriteVerbose($"No English subtitles in {fileName}"),
            Logger);
    }

    private bool TryResolveDirectoryPath(string path, bool requireExists, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (Dadstart.Labs.MediaForge.Services.System.PathResolver.TryResolveProviderPath(this, path, out var fromProvider))
        {
            resolvedPath = fromProvider!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        if (Dadstart.Labs.MediaForge.Services.System.PathResolver.TryGetUnresolvedProviderPath(this, path, out var unresolved))
        {
            resolvedPath = unresolved!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        return false;
    }
}

/// <summary>
/// Result of converting a single video file from a batch.
/// </summary>
public record VideoFileConversionResult(string InputPath, string OutputPath, bool Success, string Status);
