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
/// Converts bonus MKV files, extracts subtitles, and organizes them into Plex-style bonus content folders.
/// </summary>
/// <remarks>
/// Three-step workflow: (1) convert bonus MKV files (names ending with -trailer, -featurette, etc.) to MP4,
/// (2) extract English subtitles and optionally OCR image-based tracks (-Ocr Auto/Skip/Force),
/// (3) move converted MP4 and matching .srt/.vtt files into Plex bonus folders under OutputPath.
/// Existing destination files are skipped.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "BonusFileProcessing")]
[OutputType(typeof(void))]
public class InvokeBonusFileProcessingCommand : ProgressCmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    private readonly List<ConversionSummary> _conversionResults = new();
    private readonly List<(long FileSizeBytes, TimeSpan ProcessingTime)> _fileProcessingStats = new();

    private IMediaReaderService? _mediaReaderService;
    private IMediaConversionService? _mediaConversionService;
    private IPathResolver? _pathResolverService;
    private IExecutableService? _executableService;

    private List<(string Path, long Size)>? _sizedBonusFiles;
    private Stopwatch? _conversionBatchStopwatch;
    private int _conversionCurrentFileIndex;
    private int _conversionBatchTotalFiles;
    private long _conversionBatchTotalBytes;
    private long _conversionBatchCompletedBytes;

    private static readonly (string FolderName, string Suffix)[] _plexLayout =
    {
        ("Behind The Scenes", "behindthescenes"),
        ("Deleted Scenes", "deleted"),
        ("Featurettes", "featurette"),
        ("Interviews", "interview"),
        ("Scenes", "scene"),
        ("Shorts", "short"),
        ("Trailers", "trailer"),
        ("Other", "other")
    };

    private static readonly string[] _subtitleExtensions = { "srt", "vtt" };

    /// <summary>
    /// Source directory containing media files to process.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        HelpMessage = "Source directory containing media files to process")]
    [ValidateNotNullOrEmpty]
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Destination directory for organized Plex files.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        HelpMessage = "Destination directory for organized Plex files")]
    [ValidateNotNullOrEmpty]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Default encoder to use: 'x264' (libx264), 'x265' (libx265), or 'nvenc' (NVENC HEVC).
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Default encoder to use when converting bonus files: 'x264', 'x265', or 'nvenc'")]
    [ValidateSet("x264", "x265", "nvenc", IgnoreCase = true)]
    public string DefaultVideoEncoder { get; set; } = "nvenc";

    /// <summary>
    /// When specified, skips extracting subtitles from bonus files.
    /// </summary>
    [Parameter(HelpMessage = "Skip subtitle extraction from bonus files.")]
    public SwitchParameter SkipSubtitles { get; set; }

    /// <summary>
    /// Controls OCR of image-based subtitles (SUP, SUB). Default is Auto. Skip leaves exported subtitles unchanged; Force OCRs all image subtitle files; Auto OCRs image subtitles when the source has a single exported subtitle format and it is not SRT.
    /// </summary>
    [Parameter(HelpMessage = "OCR mode for image subtitles: Auto, Skip, or Force.")]
    [ValidateSet(SubtitleOcrMode.Auto, SubtitleOcrMode.Skip, SubtitleOcrMode.Force, IgnoreCase = true)]
    public string Ocr { get; set; } = SubtitleOcrMode.Default;

    /// <summary>
    /// When specified, skips repair of OCR-produced SRT files after extraction or OCR.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair after extraction or OCR.")]
    public SwitchParameter SkipRepair { get; set; }

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Only used when repair runs.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy SRT files to before repairing; preserves path structure.")]
    public string? BackupPath { get; set; }

    /// <summary>
    /// Maximum number of image-to-SRT conversions to run in parallel when -Ocr is Force or Auto. Default is 10.
    /// </summary>
    [Parameter(HelpMessage = "Maximum number of image subtitle conversions to run simultaneously when OCR is enabled.")]
    public int ThrottleLimit { get; set; } = 10;

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    private IMediaConversionService MediaConversionService => _mediaConversionService ??= ModuleServices.GetRequiredService<IMediaConversionService>();

    private IPathResolver PathResolverService => _pathResolverService ??= ModuleServices.GetRequiredService<IPathResolver>();

    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();

    /// <summary>
    /// Executes the bonus file processing workflow.
    /// </summary>
    protected override void Process()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath))
            return;

        if (!TryResolveDirectoryPath(InputPath, requireExists: true, out var inputFullPath))
        {
            WriteError(CreateErrorRecord(
                new DirectoryNotFoundException($"Input path does not exist or could not be resolved: '{InputPath}'"),
                "InputPathNotFound",
                ErrorCategory.InvalidArgument,
                InputPath));
            return;
        }

        if (!TryResolveOutputPath(PathResolverService, OutputPath, out var outputFullPath))
            return;

        WriteHostMessage("Starting Bonus File Processing", ConsoleColor.Cyan);
        WriteHostMessage($"  Input:  {inputFullPath}", ConsoleColor.Gray);
        WriteHostMessage($"  Output: {outputFullPath}", ConsoleColor.Gray);

        // Ensure output directory exists
        Directory.CreateDirectory(outputFullPath);
        WriteHostMessage($"Output path ready: {outputFullPath}", ConsoleColor.Green);

        _conversionResults.Clear();

        int bonusFileCount;
        try
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage("Step 1: Converting media files...", ConsoleColor.Cyan);
            bonusFileCount = ConvertBonusFiles(inputFullPath);

            WriteHostMessage("Media files converted successfully", ConsoleColor.Green);

            if (_conversionResults.Count > 0)
                WriteConversionSummary();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to convert bonus media files");
            WriteError(new ErrorRecord(
                ex,
                "BonusConversionFailed",
                ErrorCategory.OperationStopped,
                inputFullPath));
            WriteWarning("Continuing with file organization for Plex despite conversion error.");
            bonusFileCount = _conversionResults.Count(summary => summary.Success);
        }

        if (!SkipSubtitles.IsPresent)
        {
            try
            {
                WriteHostMessage(string.Empty);
                WriteHostMessage("Step 2: Extracting subtitles from bonus files...", ConsoleColor.Cyan);
                var exportedPaths = ExtractSubtitlesFromBonusFiles(inputFullPath);
                if (exportedPaths.Count > 0 && SubtitleOcrMode.RequiresOcrProcessing(Ocr))
                {
                    var imagePaths = SubtitlePathHelper.SelectImagePathsForOcr(exportedPaths, Ocr);
                    if (imagePaths.Count > 0)
                    {
                        var srtPaths = SubtitlePathHelper.GetSrtPaths(exportedPaths);
                        SubtitleOcrRepairWorkflow.Run(
                            CmdletIO,
                            Logger,
                            ExecutableService,
                            PathResolverService,
                            imagePaths,
                            srtPaths,
                            performOcr: true,
                            ThrottleLimit,
                            shouldRepair: SubtitleOcrMode.ShouldRepair(Ocr, SkipRepair.IsPresent),
                            BackupPath,
                            StoppingToken);
                    }
                }
                WriteHostMessage("Subtitle extraction completed", ConsoleColor.Green);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to extract or process subtitles from bonus files");
                WriteWarning($"Continuing with file organization despite subtitle error: {ex.Message}");
            }
        }

        try
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage("Step 3: Organizing files for Plex...", ConsoleColor.Cyan);
            InvokePlexFileOperation(inputFullPath, outputFullPath);
            WriteHostMessage("Files successfully organized and moved to Plex location", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to organize files for Plex");
            var error = new ErrorRecord(
                ex,
                "PlexOrganizationFailed",
                ErrorCategory.OperationStopped,
                outputFullPath);
            ThrowTerminatingError(error);
            return;
        }

        WriteHostMessage(string.Empty);
        WriteHostMessage("Bonus File Processing completed successfully!", ConsoleColor.Green);
        WriteHostMessage($"  Bonus files processed: {bonusFileCount}", ConsoleColor.Gray);
    }

    /// <summary>
    /// Resolves a directory path using the PowerShell session's current location.
    /// </summary>
    /// <param name="path">The path to resolve (e.g. ".", "P:\Movies\...").</param>
    /// <param name="requireExists">If true, resolved path must exist as a directory (used for input).</param>
    /// <param name="resolvedPath">The resolved full path.</param>
    /// <returns>True if resolution succeeded and, when requireExists is true, the directory exists.</returns>
    private bool TryResolveDirectoryPath(string path, bool requireExists, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (PathResolver.TryResolveProviderPath(CmdletIO.Paths, path, out var fromProvider))
        {
            resolvedPath = fromProvider!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        if (PathResolver.TryGetUnresolvedProviderPath(CmdletIO.Paths, path, out var unresolved))
        {
            resolvedPath = unresolved!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        return false;
    }

    private int ConvertBonusFiles(string inputDirectory)
    {
        var bonusSuffixes = _plexLayout.Select(p => p.Suffix).ToArray();
        var allMkvFiles = Directory.EnumerateFiles(inputDirectory, "*.mkv", SearchOption.TopDirectoryOnly)
            .ToList();

        var bonusFiles = allMkvFiles
            .Where(path =>
            {
                var baseName = Path.GetFileNameWithoutExtension(path);
                return bonusSuffixes.Any(suffix =>
                    baseName.EndsWith($"-{suffix}", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        var bonusFileCount = bonusFiles.Count;

        if (bonusFileCount == 0)
        {
            var suffixList = string.Join(", ", bonusSuffixes);
            WriteHostMessage($"No bonus-suffix MKV files to convert (suffixes: {suffixList})", ConsoleColor.Gray);
            return 0;
        }

        var bonusFilesWithSize = MediaConversionHelper.BuildItemsWithSizes(bonusFiles, static path => path, out var totalBytes)
            .Select(entry => (Path: entry.Item, entry.Size))
            .ToList();

        WriteHostMessage($"Converting {bonusFileCount} bonus file(s) (total size: {MediaConversionHelper.FormatByteCount(totalBytes)})", ConsoleColor.Cyan);

        _fileProcessingStats.Clear();
        _sizedBonusFiles = bonusFilesWithSize;
        _conversionBatchTotalBytes = totalBytes;
        _conversionBatchTotalFiles = bonusFileCount;
        _conversionBatchCompletedBytes = 0;
        _conversionCurrentFileIndex = 0;
        _conversionBatchStopwatch = Stopwatch.StartNew();

        foreach (var (filePath, fileSize) in bonusFilesWithSize)
        {
            _conversionCurrentFileIndex++;
            var fileName = Path.GetFileName(filePath);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                _conversionCurrentFileIndex,
                _conversionBatchTotalFiles,
                fileName,
                _conversionBatchCompletedBytes,
                _conversionBatchTotalBytes);
            var remainingBytes = CalculateConversionRemainingBytes(filePath, _conversionBatchTotalFiles - _conversionCurrentFileIndex);
            var eta = remainingBytes.HasValue
                ? MediaConversionHelper.CalculateRemainingTime(remainingBytes.Value, _fileProcessingStats)
                : null;

            MediaConversionHelper.WriteMainProgress(CmdletIO, "Bonus file conversion", status, percent, eta, ProgressRecordType.Processing);

            var summary = ConvertSingleBonusFile(filePath, inputDirectory);

            _conversionResults.Add(summary);
            if (summary.Success)
            {
                _conversionBatchCompletedBytes += fileSize;
                _fileProcessingStats.Add((fileSize, summary.ProcessingTime));
            }

            (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                _conversionCurrentFileIndex,
                _conversionBatchTotalFiles,
                fileName,
                _conversionBatchCompletedBytes,
                _conversionBatchTotalBytes);
            MediaConversionHelper.WriteMainProgress(CmdletIO, "Bonus file conversion", status, percent, null, ProgressRecordType.Processing);
        }

        _conversionBatchStopwatch?.Stop();
        MediaConversionHelper.WriteProgressCompleted(CmdletIO, "Bonus file conversion", "Current file");

        return bonusFileCount;
    }

    private long? CalculateConversionRemainingBytes(string currentFilePath, int remainingFilesCount)
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

        if (_sizedBonusFiles == null)
            return null;

        var remainingPaths = _sizedBonusFiles.Skip(_conversionCurrentFileIndex).Take(remainingFilesCount);
        foreach (var entry in remainingPaths)
            remainingBytes += entry.Size;

        return remainingBytes;
    }

    private ConversionSummary ConvertSingleBonusFile(string inputFilePath, string inputDirectory)
    {
        var fileName = Path.GetFileName(inputFilePath);
        var stopwatch = Stopwatch.StartNew();
        Logger.LogInformation("Processing bonus file: {InputFilePath}", inputFilePath);
        WriteVerbose($"Processing bonus file: {inputFilePath}");
        UpdateFileProgress($"Preparing {fileName}", fileName, percentComplete: 0);

        try
        {
            UpdateFileProgress("Reading media metadata", fileName);
            var mediaFile = MediaReaderService.GetMediaFileAsync(inputFilePath, StoppingToken)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (mediaFile == null)
            {
                const string StatusMessage = "Failed to read media file information";
                WriteWarning($"{StatusMessage}: {inputFilePath}");
                stopwatch.Stop();
                UpdateFileProgress(StatusMessage, fileName, recordType: ProgressRecordType.Completed);
                return new ConversionSummary(inputFilePath, false, StatusMessage, stopwatch.Elapsed);
            }

            UpdateFileProgress("Building audio track mappings", fileName);
            AudioTrackMapping[] audioMappings;
            var audioSelection = MediaConversionHelper.SelectPreferredAudioStreams(mediaFile.Streams);
            if (audioSelection.TotalAudioStreamCount == 0)
            {
                Logger.LogInformation("No audio streams found in bonus file: {InputFilePath}, processing as video-only", inputFilePath);
                audioMappings = Array.Empty<AudioTrackMapping>();
            }
            else
            {
                if (audioSelection.EnglishAudioStreamCount == 0)
                    Logger.LogInformation("No English audio streams found in bonus file: {InputFilePath}, using all audio streams", inputFilePath);

                try
                {
                    audioMappings = MediaConversionHelper.CreateAutomaticAudioTrackMappings(audioSelection.SelectedStreams);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to create audio track mappings for bonus file: {InputFilePath}", inputFilePath);
                    var message = $"Audio settings can't be auto-detected for: {inputFilePath}. Error: {ex.Message}";
                    WriteWarning(message);
                    stopwatch.Stop();
                    UpdateFileProgress("Failed to build audio mappings", fileName, recordType: ProgressRecordType.Completed);
                    return new ConversionSummary(inputFilePath, false, message, stopwatch.Elapsed);
                }
            }

            var videoSettings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(DefaultVideoEncoder);
            var x265Arguments = MediaConversionHelper.BuildX265Arguments(null, videoSettings.Codec);

            var outputFileName = Path.GetFileNameWithoutExtension(inputFilePath) + ".mp4";
            var outputFilePath = Path.Combine(inputDirectory, outputFileName);

            try
            {
                RunConversionWithProgress(
                    inputFilePath,
                    outputFilePath,
                    videoSettings,
                    audioMappings,
                    x265Arguments,
                    outputFileName);

                stopwatch.Stop();
                UpdateFileProgress("Conversion completed", fileName, recordType: ProgressRecordType.Completed);
                return new ConversionSummary(inputFilePath, true, "Success", stopwatch.Elapsed);
            }
            catch (FfmpegConversionException ex)
            {
                stopwatch.Stop();
                var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
                UpdateFileProgress("Conversion failed", fileName, recordType: ProgressRecordType.Completed);
                return new ConversionSummary(inputFilePath, false, statusMessage, stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                UpdateFileProgress("Error", fileName, recordType: ProgressRecordType.Completed);
                return new ConversionSummary(inputFilePath, false, $"Conversion failed: {ex.Message}", stopwatch.Elapsed);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Failed to read media file for bonus processing: {InputFilePath}", inputFilePath);
            var message = $"Failed to read media file: {ex.Message}";
            WriteWarning($"{message} ({inputFilePath})");
            UpdateFileProgress("Error", fileName, recordType: ProgressRecordType.Completed);
            return new ConversionSummary(inputFilePath, false, message, stopwatch.Elapsed);
        }
    }

    private void RunConversionWithProgress(
        string inputFilePath,
        string outputFilePath,
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        string[]? additionalArguments,
        string outputFileName)
    {
        try
        {
            Logger.LogInformation(
                "Starting bonus media file conversion: {InputFilePath} -> {OutputFilePath}",
                inputFilePath,
                outputFilePath);

            var encodeStatus = $"Encoding to {videoSettings.Codec} ({videoSettings.Preset} preset)";
            UpdateFileProgress(encodeStatus, outputFileName, percentComplete: 0);

            var encodeProgress = new LatestFfmpegProgress();
            var spinner = new[] { "|", "/", "-", "\\" };
            var spinnerIndex = 0;
            var lastBatchUpdateTime = DateTime.UtcNow;
            var encodeStartElapsed = _conversionBatchStopwatch?.Elapsed ?? TimeSpan.Zero;
            var conversionTask = Task.Run(() => MediaConversionService.ExecuteConversion(
                inputFilePath,
                outputFilePath,
                videoSettings,
                audioMappings,
                additionalArguments,
                encodeProgress,
                StoppingToken));

            TimeSpan? initialBatchEta = null;
            if (_conversionCurrentFileIndex <= _conversionBatchTotalFiles)
            {
                var remainingBytes = CalculateConversionRemainingBytes(
                    inputFilePath,
                    _conversionBatchTotalFiles - _conversionCurrentFileIndex);
                if (remainingBytes.HasValue)
                    initialBatchEta = MediaConversionHelper.CalculateRemainingTime(remainingBytes.Value, _fileProcessingStats);
            }

            while (!conversionTask.Wait(TimeSpan.FromSeconds(0.05)))
            {
                StoppingToken.ThrowIfCancellationRequested();

                var latest = encodeProgress.Latest;
                if (latest is not null)
                {
                    var (status, eta) = MediaConversionHelper.BuildEncodeProgressDisplay(
                        encodeStatus,
                        latest,
                        spinner,
                        ref spinnerIndex);
                    UpdateFileProgress(
                        status,
                        outputFileName,
                        percentComplete: latest.PercentComplete,
                        eta: eta);
                }
                else
                {
                    var indicator = spinner[spinnerIndex];
                    spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                    UpdateFileProgress($"{encodeStatus} {indicator}", outputFileName, percentComplete: 0);
                }

                var now = DateTime.UtcNow;
                if ((now - lastBatchUpdateTime).TotalSeconds >= 1.0 && initialBatchEta.HasValue && _conversionBatchStopwatch != null)
                {
                    var remaining = initialBatchEta.Value - (_conversionBatchStopwatch.Elapsed - encodeStartElapsed);
                    if (remaining.TotalSeconds > 0)
                    {
                        var fileName = Path.GetFileName(inputFilePath);
                        var (batchStatus, batchPercent) = MediaConversionHelper.BuildBatchProgressStatus(
                            _conversionCurrentFileIndex,
                            _conversionBatchTotalFiles,
                            fileName,
                            _conversionBatchCompletedBytes,
                            _conversionBatchTotalBytes);
                        MediaConversionHelper.WriteMainProgress(
                            CmdletIO,
                            "Bonus file conversion",
                            batchStatus,
                            batchPercent,
                            remaining,
                            ProgressRecordType.Processing);
                    }

                    lastBatchUpdateTime = now;
                }
            }

            conversionTask.GetAwaiter().GetResult();
            Logger.LogInformation(
                "Successfully converted bonus media file: {InputFilePath} -> {OutputFilePath}",
                inputFilePath,
                outputFilePath);
        }
        catch (FfmpegConversionException ex)
        {
            Logger.LogError(
                ex,
                "FFmpeg conversion failed for bonus media file: {InputFilePath} -> {OutputFilePath}",
                inputFilePath,
                outputFilePath);
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
            Logger.LogError(
                ex,
                "Exception occurred while converting bonus media file: {InputFilePath} -> {OutputFilePath}",
                inputFilePath,
                outputFilePath);
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
        MediaConversionHelper.WriteCurrentItemProgress(
            CmdletIO,
            "Current file",
            status,
            currentOperation,
            percentComplete,
            eta,
            recordType);

    private static List<string> GetBonusMkvPaths(string inputDirectory)
    {
        var bonusSuffixes = _plexLayout.Select(p => p.Suffix).ToArray();
        var allMkvFiles = Directory.EnumerateFiles(inputDirectory, "*.mkv", SearchOption.TopDirectoryOnly);
        return allMkvFiles
            .Where(path =>
            {
                var baseName = Path.GetFileNameWithoutExtension(path);
                return bonusSuffixes.Any(suffix =>
                    baseName.EndsWith($"-{suffix}", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
    }

    private List<string> ExtractSubtitlesFromBonusFiles(string inputDirectory)
    {
        var bonusMkvPaths = GetBonusMkvPaths(inputDirectory);
        if (bonusMkvPaths.Count == 0)
            return new List<string>();

        WriteHostMessage($"Extracting subtitles from {bonusMkvPaths.Count} bonus file(s)...", ConsoleColor.Cyan);
        var exportedPaths = new List<string>();
        var mkvextractPath = WindowsExecutablePathHelper.GetMkvextractPath();

        foreach (var mkvPath in bonusMkvPaths)
        {
            var fileName = Path.GetFileName(mkvPath);
            MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "Subtitle extraction", $"Extracting... - {fileName}", recordType: ProgressRecordType.Processing);

            MediaFile? mediaFile;
            try
            {
                mediaFile = MediaReaderService.GetMediaFileAsync(mkvPath, StoppingToken)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not read media file for subtitle extraction: {Path}", mkvPath);
                continue;
            }

            if (mediaFile == null)
                continue;

            var extracted = SubtitleExportHelper.ExtractEnglishSubtitles(
                ExecutableService,
                mediaFile,
                mkvextractPath,
                buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                    mediaFile.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
                finalizeOutputPath: candidate => TryResolveOutputPath(PathResolverService, candidate, out var resolved) ? resolved : null,
                onUnknownCodec: stream => WriteWarning($"Unknown codec: {stream.Codec} - using .bin extension"),
                onExtractFailed: (_, ex) => WriteStandardError(ex, ErrorIds.SubtitleExportFailed, ErrorCategory.OperationStopped, mediaFile.Path),
                onNoEnglishSubtitles: () => WriteVerbose($"No English subtitles in {fileName}"),
                Logger,
                StoppingToken);

            foreach (var path in extracted)
            {
                WriteVerbose($"Extracted {Path.GetFileName(path)}");
                exportedPaths.Add(path);
            }

            MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "Subtitle extraction", $"Completed - {fileName}", recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(CmdletIO, "Subtitle extraction", "Current file");
        return exportedPaths;
    }

    private void WriteConversionSummary()
    {
        var succeeded = _conversionResults.Where(r => r.Success).ToList();
        var failed = _conversionResults.Where(r => !r.Success).ToList();

        if (succeeded.Count > 0)
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage($"  ✅ Succeeded ({succeeded.Count}):", ConsoleColor.Green);
            foreach (var result in succeeded)
                WriteHostMessage($"    {result.FilePath}", ConsoleColor.Gray);
        }

        if (failed.Count > 0)
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage($"  ❌ Failed ({failed.Count}):", ConsoleColor.Red);
            foreach (var result in failed)
                WriteHostMessage($"    {result.FilePath}", ConsoleColor.Gray);
        }
    }

    private void InvokePlexFileOperation(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(destinationDirectory))
            throw new DirectoryNotFoundException($"Destination folder does not exist: '{destinationDirectory}'");

        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Source folder does not exist: '{sourceDirectory}'");

        AddPlexFolders(destinationDirectory);
        MovePlexFiles(sourceDirectory, destinationDirectory);
        RemovePlexEmptyFolders(destinationDirectory);
    }

    private static void AddPlexFolders(string destinationDirectory)
    {
        foreach (var (folderName, _) in _plexLayout)
        {
            var path = Path.Combine(destinationDirectory, folderName);
            if (Directory.Exists(path))
                continue;

            Directory.CreateDirectory(path);
        }
    }

    private void MovePlexFiles(string sourceDirectory, string destinationDirectory)
    {
        var filesMoved = 0;
        var moveCandidates = new List<(string SourceFile, string DestinationFolder, long FileSizeBytes)>();
        long totalBytes = 0;

        foreach (var (folderName, suffix) in _plexLayout)
        {
            var destFolder = Path.Combine(destinationDirectory, folderName);
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            var videoPattern = $"*-{suffix}.mp4";

            var videoFiles = Directory.EnumerateFiles(sourceDirectory, videoPattern, SearchOption.AllDirectories);
            var subtitleFiles = _subtitleExtensions
                .SelectMany(ext => Directory.EnumerateFiles(sourceDirectory, $"*-{suffix}.*{ext}", SearchOption.AllDirectories));
            var sourceFiles = videoFiles.Concat(subtitleFiles).ToList();

            if (sourceFiles.Count > 0)
                WriteHostMessage($"Moving {sourceFiles.Count} files -{suffix} to {destFolder}");

            foreach (var sourceFile in sourceFiles)
            {
                var fileSizeBytes = GetFileSizeOrZero(sourceFile);
                totalBytes += fileSizeBytes;
                moveCandidates.Add((sourceFile, destFolder, fileSizeBytes));
            }
        }

        if (moveCandidates.Count == 0)
        {
            WriteWarning($"No bonus content files found to move in source directory {sourceDirectory}");
            return;
        }

        WriteHostMessage($"Moving {moveCandidates.Count} Plex file(s) (total size: {MediaConversionHelper.FormatByteCount(totalBytes)})", ConsoleColor.Cyan);

        long completedBytes = 0;
        var currentFileIndex = 0;
        foreach (var (sourceFile, destFolder, fileSizeBytes) in moveCandidates)
        {
            currentFileIndex++;
            var fileName = Path.GetFileName(sourceFile);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                currentFileIndex,
                moveCandidates.Count,
                fileName,
                completedBytes,
                totalBytes);

            MediaConversionHelper.WriteMainProgress(CmdletIO, "Plex file organization", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "Current move file", $"Moving... - {fileName}", recordType: ProgressRecordType.Processing);

            var destinationPath = Path.Combine(destFolder, fileName);
            var currentFileStatus = "Completed";
            try
            {
                if (File.Exists(destinationPath))
                {
                    WriteWarning($"Destination file already exists, skipping: {destinationPath}");
                    currentFileStatus = "Skipped";
                }
                else
                {
                    WriteVerbose($"Moving {sourceFile} to {destFolder}");
                    File.Copy(sourceFile, destinationPath);
                    try
                    {
                        File.Delete(sourceFile);
                    }
                    catch (Exception deleteEx)
                    {
                        throw new InvalidOperationException(
                            $"Copied file to destination but failed to remove source: {deleteEx.Message}",
                            deleteEx);
                    }

                    filesMoved++;
                }
            }
            catch (Exception ex)
            {
                currentFileStatus = "Failed";
                Logger.LogWarning(
                    ex,
                    "Failed to move bonus file from {SourceFile} to {DestinationPath}",
                    sourceFile,
                    destinationPath);
                WriteError(new ErrorRecord(
                    ex,
                    "PlexMoveFailed",
                    ErrorCategory.WriteError,
                    sourceFile));
            }
            finally
            {
                completedBytes += fileSizeBytes;
                (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                    currentFileIndex,
                    moveCandidates.Count,
                    fileName,
                    completedBytes,
                    totalBytes);
                MediaConversionHelper.WriteMainProgress(CmdletIO, "Plex file organization", status, percent, recordType: ProgressRecordType.Processing);
                MediaConversionHelper.WriteCurrentItemProgress(CmdletIO, "Current move file", $"{currentFileStatus} - {fileName}", recordType: ProgressRecordType.Completed);
            }
        }

        MediaConversionHelper.WriteProgressCompleted(CmdletIO, "Plex file organization", "Current move file");

        if (filesMoved == 0)
            WriteWarning($"No bonus content files found to move in source directory {sourceDirectory}");
        else
            WriteVerbose($"{filesMoved} files moved to Plex folders");
    }

    private static long GetFileSizeOrZero(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            return fileInfo.Exists ? fileInfo.Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void RemovePlexEmptyFolders(string destinationDirectory)
    {
        var foldersDeleted = 0;

        foreach (var (folderName, _) in _plexLayout)
        {
            var path = Path.Combine(destinationDirectory, folderName);
            if (!Directory.Exists(path))
                continue;

            if (Directory.EnumerateFileSystemEntries(path).Any())
                continue;

            try
            {
                WriteVerbose($"Removing empty Plex folder: {path}");
                Directory.Delete(path);
                foldersDeleted++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to remove empty Plex folder: {FolderPath}", path);
                WriteError(new ErrorRecord(
                    ex,
                    "PlexFolderRemovalFailed",
                    ErrorCategory.WriteError,
                    path));
            }
        }

        if (foldersDeleted == 0)
            WriteWarning($"No empty Plex folders found to remove in '{destinationDirectory}'");
        else
            WriteVerbose($"{foldersDeleted} empty Plex folders deleted");
    }

    private readonly struct ConversionSummary
    {
        public ConversionSummary(string filePath, bool success, string status, TimeSpan processingTime)
        {
            FilePath = filePath;
            Success = success;
            Status = status;
            ProcessingTime = processingTime;
        }

        public string FilePath { get; }

        public bool Success { get; }

        public string Status { get; }

        public TimeSpan ProcessingTime { get; }
    }
}
