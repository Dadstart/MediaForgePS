using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Converts video files in a directory (or specified paths) to MP4 with automatic English audio mapping and optional caption extraction.
/// </summary>
/// <remarks>
/// Primary batch conversion cmdlet for video libraries. Supports common container extensions (.mkv, .mp4, .mov, .avi, .webm, and more).
/// Default video encoder is nvenc. After each successful conversion, English subtitle streams are extracted unless -SkipSubtitles is specified.
/// Use -Ocr Auto, Skip, or Force to control OCR of image-based captions (SUP, SUB) after extraction.
/// Writes a <see cref="MediaConversionResult"/> per processed file to the pipeline.
/// When captions are extracted, also writes a <see cref="SubtitleProcessingResult"/> with extract/OCR counts.
/// Supports -WhatIf and -Confirm.
/// </remarks>
[Cmdlet(VerbsData.Convert, "VideoFile", SupportsShouldProcess = true)]
[OutputType(typeof(MediaConversionResult))]
[OutputType(typeof(SubtitleProcessingResult))]
public class ConvertVideoFileCommand : ProgressCmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    private IPathResolver? _pathResolver;
    private IMediaReaderService? _mediaReaderService;
    private IAudioTrackMappingService? _audioTrackMappingService;
    private IMediaConversionService? _mediaConversionService;
    private readonly List<MediaConversionResult> _results = new();
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
    /// Controls OCR of image-based captions (SUP, SUB). Default is Auto. Skip leaves exported subtitles unchanged; Force OCRs all image subtitle files; Auto OCRs image subtitles when the source has a single exported subtitle format and it is not SRT. Has no effect when -SkipSubtitles is specified.
    /// </summary>
    [Parameter(HelpMessage = "OCR mode for image captions: Auto, Skip, or Force.")]
    [ValidateSet(SubtitleOcrMode.Auto, SubtitleOcrMode.Skip, SubtitleOcrMode.Force, IgnoreCase = true)]
    public string Ocr { get; set; } = SubtitleOcrMode.Default;

    /// <summary>
    /// When specified, skips repair of OCR-produced SRT files. Has no effect when -Ocr is Skip.
    /// </summary>
    [Parameter(HelpMessage = "Skip repair of OCR-produced SRT files.")]
    public SwitchParameter SkipRepair { get; set; }

    /// <summary>
    /// Keeps source .sup/.sub/.idx files after successful OCR conversion, and keeps unused image sidecars Auto would otherwise discard when a text SRT is already present. By default they are deleted.
    /// </summary>
    [Parameter(HelpMessage = "Keep source image subtitle files after successful OCR conversion.")]
    public SwitchParameter KeepSource { get; set; }

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
            CmdletIO.Paths,
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
        var outputDirectoryEnsured = false;

        foreach (var (inputPath, inputRoot, outputRoot, fileSize) in _sizedVideoFiles)
        {
            _currentFileIndex++;
            var fileName = GetFileName(inputPath);
            if (!ShouldProcess($"Convert '{fileName}'", "Convert video file"))
            {
                Logger.LogInformation("WhatIf: Would convert '{InputFileName}'", fileName);
                continue;
            }

            if (!outputDirectoryEnsured && !string.IsNullOrWhiteSpace(configuredOutputDirectory))
            {
                Directory.CreateDirectory(configuredOutputDirectory);
                outputDirectoryEnsured = true;
            }

            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                _currentFileIndex, _batchTotalFiles, fileName, _batchCompletedBytes, _batchTotalBytes);
            var batchEta = CalculateBatchRemainingTime(inputPath, _batchTotalFiles - _currentFileIndex);
            MediaConversionHelper.WriteMainProgress(
                CmdletIO, "Video file conversion", status, percent, batchEta, ProgressRecordType.Processing);

            var result = ConvertSingleFile(
                inputRoot,
                outputRoot,
                inputPath,
                fileName,
                videoSettings,
                additionalArguments);

            _results.Add(result);
            WriteObject(result);

            if (MediaConversionHelper.IsCompletedConversion(result))
                _batchCompletedBytes += fileSize;
        }

        _batchStopwatch?.Stop();
        MediaConversionHelper.WriteProgressCompleted(CmdletIO, "Video file conversion", "File conversion");

        if (!SkipSubtitles.IsPresent)
        {
            var successes = _results.Where(MediaConversionHelper.IsCompletedConversion).ToList();
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
                    MediaConversionHelper.WriteMainProgress(CmdletIO, "Caption extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
                    MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "Current file", "Extracting captions...", name, recordType: ProgressRecordType.Processing);

                    extractedCaptionPaths.AddRange(ExtractEnglishSubtitlesToOutputSidecars(r.InputPath, r.OutputPath));

                    (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, name);
                    MediaConversionHelper.WriteMainProgress(CmdletIO, "Caption extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
                    MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "Current file", "Completed", name, recordType: ProgressRecordType.Completed);
                }

                MediaConversionHelper.WriteProgressCompleted(CmdletIO, "Caption extraction", "Current file");
                WriteVerbose($"Caption extraction - files: {total}, paths: {extractedCaptionPaths.Count}.");

                IReadOnlyList<string> convertedPaths = Array.Empty<string>();
                if (SubtitleOcrMode.RequiresOcrProcessing(Ocr) && extractedCaptionPaths.Count > 0)
                {
                    var imagePaths = SubtitlePathHelper.SelectImagePathsForOcr(extractedCaptionPaths, Ocr);
                    if (imagePaths.Count > 0)
                    {
                        var srtPathsFromCaptions = SubtitlePathHelper.GetSrtPaths(extractedCaptionPaths);
                        WriteHostMessage("  Running OCR and repair on extracted captions...", ConsoleColor.Cyan);

                        var ocrResult = SubtitleOcrRepairWorkflow.Run(
                            CmdletIO,
                            Logger,
                            ExecutableService,
                            PathResolver,
                            imagePaths,
                            srtPathsFromCaptions,
                            performOcr: true,
                            DefaultOcrThrottleLimit,
                            shouldRepair: SubtitleOcrMode.ShouldRepair(Ocr, SkipRepair.IsPresent),
                            backupPath: null,
                            StoppingToken,
                            KeepSource.IsPresent);

                        if (ocrResult == null)
                        {
                            WriteSubtitleProcessingResult(extractedCaptionPaths, convertedPaths);
                            return;
                        }

                        convertedPaths = ocrResult.ConvertedSrtPaths;
                        WriteHostMessage("  Caption OCR and repair completed.", ConsoleColor.Green);
                    }

                    // Auto skips OCR when a text SRT coexists with VobSub/SUP (common on DVD MKVs).
                    // Remove those unused image sidecars (.sub/.idx/.sup) unless -KeepSource.
                    ImageSubtitleConversionHelper.DeleteUnusedImageSubtitleSources(
                        extractedCaptionPaths,
                        Ocr,
                        KeepSource.IsPresent,
                        Logger);
                }

                WriteSubtitleProcessingResult(extractedCaptionPaths, convertedPaths);
            }
        }

        var ok = _results.Count(MediaConversionHelper.IsCompletedConversion);
        var failed = _results.Count - ok;
        if (failed == 0)
            WriteHostMessage($"Directory conversion finished: {ok} file(s) OK.", ConsoleColor.Green);
        else
            WriteHostMessage($"Directory conversion finished: {ok} succeeded, {failed} failed.", ConsoleColor.Yellow);
    }

    private void WriteSubtitleProcessingResult(IReadOnlyList<string> extractedPaths, IReadOnlyList<string> convertedPaths)
    {
        var result = SubtitleProcessingResult.Create(extractedPaths, convertedPaths);
        WriteHostMessage(
            $"  Subtitles: {result.ExtractedCount} extracted, {result.ConvertedCount} converted.",
            ConsoleColor.Green);
        WriteObject(result);
    }

    private MediaConversionResult ConvertSingleFile(
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
                return MediaConversionHelper.CreateConversionResult(
                    inputPath, inputPath, false, "Failed to read media metadata.", _fileStopwatch.Elapsed);
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
                return MediaConversionHelper.CreateConversionResult(
                    inputPath, outputPath, false, "Failed to resolve output path.", _fileStopwatch.Elapsed);
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
            return MediaConversionHelper.CreateConversionResult(
                inputPath, resolvedOutputPath, true, MediaConversionResult.CompletedStatus, _fileStopwatch.Elapsed);
        }
        catch (FfmpegConversionException ex)
        {
            _fileStopwatch?.Stop();
            var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
            UpdateFileProgress("Conversion failed", fileName, recordType: ProgressRecordType.Completed);
            return MediaConversionHelper.CreateConversionResult(
                inputPath, inputPath, false, statusMessage, _fileStopwatch?.Elapsed ?? TimeSpan.Zero);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _fileStopwatch?.Stop();
            UpdateFileProgress("Error", fileName, recordType: ProgressRecordType.Completed);
            return MediaConversionHelper.CreateConversionResult(
                inputPath, inputPath, false, ex.Message, _fileStopwatch?.Elapsed ?? TimeSpan.Zero);
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
            var encodeStatus = $"Encoding to {videoSettings.Codec} ({videoSettings.Preset} preset)";

            TimeSpan? initialBatchEta = null;
            if (_currentFileIndex < _batchTotalFiles)
            {
                var remainingFiles = _batchTotalFiles - _currentFileIndex;
                initialBatchEta = CalculateBatchRemainingTime(resolvedInputPath, remainingFiles);
            }

            Action? reportBatchProgress = null;
            if (initialBatchEta.HasValue && _batchStopwatch != null)
            {
                var batchStopwatch = _batchStopwatch;
                var batchEta = initialBatchEta.Value;
                reportBatchProgress = () =>
                {
                    var remaining = batchEta - batchStopwatch.Elapsed;
                    if (remaining.TotalSeconds <= 0)
                        return;

                    var (batchStatus, batchPercent) = MediaConversionHelper.BuildBatchProgressStatus(
                        _currentFileIndex,
                        _batchTotalFiles,
                        GetFileName(resolvedInputPath),
                        _batchCompletedBytes,
                        _batchTotalBytes);
                    MediaConversionHelper.WriteMainProgress(
                        CmdletIO,
                        "Video file conversion",
                        batchStatus,
                        batchPercent,
                        remaining,
                        ProgressRecordType.Processing);
                };
            }

            MediaConversionHelper.RunConversionWithProgress(
                (progress, cancellationToken) => MediaConversionService.ExecuteConversion(
                    resolvedInputPath,
                    resolvedOutputPath,
                    videoSettings,
                    audioMappings,
                    additionalArguments,
                    progress,
                    cancellationToken),
                encodeStatus,
                outputFileName,
                update => UpdateFileProgress(
                    update.Status,
                    update.CurrentOperation,
                    update.PercentComplete,
                    eta: update.Eta),
                StoppingToken,
                reportBatchProgress);

            Logger.LogInformation("Converted: {Input} -> {Output}", resolvedInputPath, resolvedOutputPath);
        }
        catch (FfmpegConversionException ex)
        {
            Logger.LogError(ex, "FFmpeg conversion failed: {Input} -> {Output}", resolvedInputPath, resolvedOutputPath);
            var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
            UpdateFileProgress(statusMessage, outputFileName, recordType: ProgressRecordType.Completed);
            throw;
        }
        catch (OperationCanceledException)
        {
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
        MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "File conversion", status, currentOperation, percentComplete, eta, recordType);

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
            mediaFile = MediaReaderService.GetMediaFileAsync(sourceMkvPath, StoppingToken)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            throw;
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
            Logger,
            StoppingToken);
    }

    private bool TryResolveDirectoryPath(string path, bool requireExists, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (Dadstart.Labs.MediaForge.Services.System.PathResolver.TryResolveProviderPath(CmdletIO.Paths, path, out var fromProvider))
        {
            resolvedPath = fromProvider!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        if (Dadstart.Labs.MediaForge.Services.System.PathResolver.TryGetUnresolvedProviderPath(CmdletIO.Paths, path, out var unresolved))
        {
            resolvedPath = unresolved!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        return false;
    }
}
