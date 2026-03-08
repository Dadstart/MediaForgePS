using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Processes bonus media files and organizes them into a Plex destination.
/// </summary>
/// <remarks>
/// This cmdlet is a C# implementation of the Invoke-BonusFileProcessing PowerShell function.
/// It performs two main steps:
/// 1. Converts bonus MKV files in the input directory using the same encoder defaults as Convert-MediaFiles.
/// 2. Organizes converted bonus files into Plex bonus content folders under the output directory.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "BonusFileProcessing")]
[OutputType(typeof(void))]
public class InvokeBonusFileProcessingCommand : CmdletBase
{
    private readonly List<ConversionSummary> _conversionResults = new();
    private readonly List<BonusFileProcessingStats> _fileProcessingStats = new();

    private IMediaReaderService? _mediaReaderService;
    private IMediaConversionService? _mediaConversionService;
    private IPathResolver? _pathResolverService;
    private IExecutableService? _executableService;

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
    /// Destination directory for organized Plex files. Must be under P:\ drive on Windows.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        HelpMessage = "Destination directory for organized Plex files (must be under P:\\ on Windows)")]
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
    /// When specified, skips OCR conversion of image-based subtitles (SUP, SUB).
    /// </summary>
    [Parameter(HelpMessage = "Skip OCR conversion of image subtitles to SRT.")]
    public SwitchParameter SkipOcr { get; set; }

    /// <summary>
    /// When specified, skips the SRT repair step after extraction or OCR.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair after extraction or OCR.")]
    public SwitchParameter SkipRepair { get; set; }

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Only used when repair runs.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy SRT files to before repairing; preserves path structure.")]
    public string? BackupPath { get; set; }

    /// <summary>
    /// Maximum number of image-to-SRT conversions to run in parallel unless -SkipOcr is specified. Default is 10.
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

        if (!ValidatePlexOutputPath(outputFullPath))
            return;

        // Ensure output directory exists
        Directory.CreateDirectory(outputFullPath);
        WriteHostMessage($"Output path validated: {outputFullPath}", ConsoleColor.Green);

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
                if (exportedPaths.Count > 0)
                {
                    var imagePaths = SubtitlePathHelper.GetImageSubtitlePaths(exportedPaths);
                    var srtPaths = SubtitlePathHelper.GetSrtPaths(exportedPaths);
                    SubtitleOcrRepairWorkflow.Run(
                        this,
                        Logger,
                        ExecutableService,
                        PathResolverService,
                        imagePaths,
                        srtPaths,
                        performOcr: !SkipOcr.IsPresent,
                        ThrottleLimit,
                        shouldRepair: !SkipRepair.IsPresent,
                        BackupPath);
                }
                WriteHostMessage("Subtitle extraction completed", ConsoleColor.Green);
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

        if (PathResolver.TryResolveProviderPath(this, path, out var fromProvider))
        {
            resolvedPath = fromProvider!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        if (PathResolver.TryGetUnresolvedProviderPath(this, path, out var unresolved))
        {
            resolvedPath = unresolved!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        return false;
    }

    private bool ValidatePlexOutputPath(string outputFullPath)
    {
        // Match original script behavior on Windows: enforce P:\ drive
        if (OperatingSystem.IsWindows())
        {
            var root = Path.GetPathRoot(outputFullPath);
            if (!string.IsNullOrEmpty(root) &&
                !root.StartsWith("P:", StringComparison.OrdinalIgnoreCase))
            {
                var message = $"Output path must be under P:\\ drive. Current path: {outputFullPath}";
                var error = new ErrorRecord(
                    new InvalidOperationException(message),
                    "InvalidPlexOutputPath",
                    ErrorCategory.InvalidArgument,
                    outputFullPath);
                WriteError(error);
                return false;
            }
        }

        return true;
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
        long completedBytes = 0;
        var currentFileIndex = 0;

        foreach (var (filePath, fileSize) in bonusFilesWithSize)
        {
            currentFileIndex++;
            var fileName = Path.GetFileName(filePath);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(currentFileIndex, bonusFileCount, fileName, completedBytes, totalBytes);
            var eta = MediaConversionHelper.CalculateRemainingTime(totalBytes - completedBytes, _fileProcessingStats.Select(s => (s.FileSizeBytes, s.ProcessingTime)));

            MediaConversionHelper.WriteMainProgress(this, "Bonus file conversion", status, percent, eta, ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", $"Converting... - {fileName}", recordType: ProgressRecordType.Processing);

            var stopwatch = Stopwatch.StartNew();
            var summary = ConvertSingleBonusFile(filePath, inputDirectory);
            stopwatch.Stop();

            _conversionResults.Add(summary);
            if (summary.Success)
            {
                completedBytes += fileSize;
                RecordFileProcessingStats(fileSize, stopwatch.Elapsed);
            }

            (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(currentFileIndex, bonusFileCount, fileName, completedBytes, totalBytes);
            MediaConversionHelper.WriteMainProgress(this, "Bonus file conversion", status, percent, null, ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", $"{(summary.Success ? "Completed" : "Failed")} - {fileName}", recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(this, "Bonus file conversion", "Current file");

        return bonusFileCount;
    }

    private void RecordFileProcessingStats(long fileSizeBytes, TimeSpan processingTime)
    {
        _fileProcessingStats.Add(new BonusFileProcessingStats
        {
            FileSizeBytes = fileSizeBytes,
            ProcessingTime = processingTime
        });
    }

    private ConversionSummary ConvertSingleBonusFile(string inputFilePath, string inputDirectory)
    {
        var fileName = Path.GetFileName(inputFilePath);
        Logger.LogInformation("Processing bonus file: {InputFilePath}", inputFilePath);
        WriteVerbose($"Processing bonus file: {inputFilePath}");

        try
        {
            var mediaFile = MediaReaderService.GetMediaFileAsync(inputFilePath, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (mediaFile == null)
            {
                const string StatusMessage = "Failed to read media file information";
                WriteWarning($"{StatusMessage}: {inputFilePath}");
                return new ConversionSummary(inputFilePath, false, StatusMessage);
            }

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
                    return new ConversionSummary(inputFilePath, false, message);
                }
            }

            var videoSettings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(DefaultVideoEncoder);
            var x265Arguments = MediaConversionHelper.BuildX265Arguments(null, videoSettings.Codec);

            var outputFileName = Path.GetFileNameWithoutExtension(inputFilePath) + ".mp4";
            var outputFilePath = Path.Combine(inputDirectory, outputFileName);

            try
            {
                Logger.LogInformation(
                    "Starting bonus media file conversion: {InputFilePath} -> {OutputFilePath}",
                    inputFilePath,
                    outputFilePath);

                MediaConversionService.ExecuteConversion(
                    inputFilePath,
                    outputFilePath,
                    videoSettings,
                    audioMappings,
                    x265Arguments);

                Logger.LogInformation(
                    "Successfully converted bonus media file: {InputFilePath} -> {OutputFilePath}",
                    inputFilePath,
                    outputFilePath);
                return new ConversionSummary(inputFilePath, true, "Success");
            }
            catch (FfmpegConversionException ex)
            {
                Logger.LogError(
                    ex,
                    "FFmpeg conversion failed for bonus media file: {InputFilePath} -> {OutputFilePath}",
                    inputFilePath,
                    outputFilePath);

                var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
                return new ConversionSummary(inputFilePath, false, statusMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Exception occurred while converting bonus media file: {InputFilePath} -> {OutputFilePath}",
                    inputFilePath,
                    outputFilePath);

                var message = $"Conversion failed: {ex.Message}";
                return new ConversionSummary(inputFilePath, false, message);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read media file for bonus processing: {InputFilePath}", inputFilePath);
            var message = $"Failed to read media file: {ex.Message}";
            WriteWarning($"{message} ({inputFilePath})");
            return new ConversionSummary(inputFilePath, false, message);
        }
    }

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
        var totalFiles = bonusMkvPaths.Count;

        for (var i = 0; i < bonusMkvPaths.Count; i++)
        {
            var mkvPath = bonusMkvPaths[i];
            var fileIndex = i + 1;
            var fileName = Path.GetFileName(mkvPath);
            MediaConversionHelper.WriteCurrentItemProgress(this, "Subtitle extraction", $"Extracting... - {fileName}", recordType: ProgressRecordType.Processing);

            MediaFile? mediaFile;
            try
            {
                mediaFile = MediaReaderService.GetMediaFileAsync(mkvPath, CancellationToken.None)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not read media file for subtitle extraction: {Path}", mkvPath);
                continue;
            }

            if (mediaFile == null)
                continue;

            var subtitles = (mediaFile.Streams ?? Array.Empty<MediaStream>())
                .Where(s => string.Equals(s.Type, "subtitle", StringComparison.OrdinalIgnoreCase) &&
                    (s.Language ?? "").StartsWith("en", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (subtitles.Count == 0)
            {
                WriteVerbose($"No English subtitles in {fileName}");
                continue;
            }

            foreach (var sub in subtitles)
            {
                if (ExportSingleSubtitle(sub, mediaFile, subtitles.Count, out var path))
                    exportedPaths.Add(path);
            }

            MediaConversionHelper.WriteCurrentItemProgress(this, "Subtitle extraction", $"Completed - {fileName}", recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(this, "Subtitle extraction", "Current file");
        return exportedPaths;
    }

    private bool ExportSingleSubtitle(MediaStream stream, MediaFile mediaFile, int totalSubtitleCount, out string resolvedOutput)
    {
        resolvedOutput = string.Empty;
        if (!SubtitleExportHelper.CodecToExtension.TryGetValue(stream.Codec ?? "", out var ext))
        {
            WriteWarning($"Unknown codec: {stream.Codec} - using .bin extension");
            ext = "bin";
        }

        var newPath = SubtitleExportHelper.GetOutputPath(mediaFile.Path, stream.Index, totalSubtitleCount, ext);
        if (!TryResolveOutputPath(PathResolverService, newPath, out var resolved))
            return false;

        resolvedOutput = resolved;

        try
        {
            SubtitleExportHelper.ExtractSubtitle(
                ExecutableService,
                stream,
                mediaFile.Path,
                resolved,
                WindowsExecutablePathHelper.GetMkvextractPath());
            WriteVerbose($"Extracted {Path.GetFileName(resolved)}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract subtitle stream {Index} from {Path}", stream.Index, mediaFile.Path);
            WriteStandardError(ex, ErrorIds.SubtitleExportFailed, ErrorCategory.OperationStopped, mediaFile.Path);
            return false;
        }
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
            var subtitlePattern = $"*-{suffix}.*srt";

            var videoFiles = Directory.EnumerateFiles(sourceDirectory, videoPattern, SearchOption.AllDirectories);
            var subtitleFiles = Directory.EnumerateFiles(sourceDirectory, subtitlePattern, SearchOption.AllDirectories);
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

            MediaConversionHelper.WriteMainProgress(this, "Plex file organization", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(this, "Current move file", $"Moving... - {fileName}", recordType: ProgressRecordType.Processing);

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
                MediaConversionHelper.WriteMainProgress(this, "Plex file organization", status, percent, recordType: ProgressRecordType.Processing);
                MediaConversionHelper.WriteCurrentItemProgress(this, "Current move file", $"{currentFileStatus} - {fileName}", recordType: ProgressRecordType.Completed);
            }
        }

        MediaConversionHelper.WriteProgressCompleted(this, "Plex file organization", "Current move file");

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
        public ConversionSummary(string filePath, bool success, string status)
        {
            FilePath = filePath;
            Success = success;
            Status = status;
        }

        public string FilePath { get; }

        public bool Success { get; }

        public string Status { get; }
    }

    private sealed class BonusFileProcessingStats
    {
        public long FileSizeBytes { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public double BytesPerSecond => FileSizeBytes > 0 && ProcessingTime.TotalSeconds > 0
            ? FileSizeBytes / ProcessingTime.TotalSeconds
            : 0;
    }
}
