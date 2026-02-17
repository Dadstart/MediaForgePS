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

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    private IMediaConversionService MediaConversionService => _mediaConversionService ??= ModuleServices.GetRequiredService<IMediaConversionService>();

    /// <summary>
    /// Executes the bonus file processing workflow.
    /// </summary>
    protected override void Process()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath))
            return;

        var inputFullPath = Path.GetFullPath(InputPath);
        var outputFullPath = Path.GetFullPath(OutputPath);

        WriteHostMessage("Starting Bonus File Processing", ConsoleColor.Cyan);
        WriteHostMessage($"  Input:  {inputFullPath}", ConsoleColor.Gray);
        WriteHostMessage($"  Output: {outputFullPath}", ConsoleColor.Gray);

        if (!Directory.Exists(inputFullPath))
        {
            var error = new ErrorRecord(
                new DirectoryNotFoundException($"Input path does not exist or is not a directory: '{inputFullPath}'"),
                "InputPathNotFound",
                ErrorCategory.InvalidArgument,
                inputFullPath);
            WriteError(error);
            return;
        }

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

        var bonusFilesWithSize = new List<(string Path, long Size)>();
        long totalBytes = 0;
        foreach (var path in bonusFiles)
        {
            long size = 0;
            try
            {
                var fi = new FileInfo(path);
                if (fi.Exists)
                {
                    size = fi.Length;
                    totalBytes += size;
                }
            }
            catch
            {
                // Use 0 for this file; total progress still reflects the rest
            }

            bonusFilesWithSize.Add((path, size));
        }

        var totalSizeFormatted = FormatByteCount(totalBytes);
        WriteHostMessage($"Converting {bonusFileCount} bonus file(s) (total size: {totalSizeFormatted})", ConsoleColor.Cyan);

        _fileProcessingStats.Clear();
        long completedBytes = 0;
        var currentFileIndex = 0;

        foreach (var (filePath, fileSize) in bonusFilesWithSize)
        {
            currentFileIndex++;
            var fileName = Path.GetFileName(filePath);
            var percent = totalBytes > 0 ? (int)((completedBytes * 100.0) / totalBytes) : 0;
            var eta = CalculateRemainingTime(completedBytes, totalBytes);

            UpdateBonusConversionProgress(
                percent,
                currentFileIndex,
                bonusFileCount,
                fileName,
                totalBytes,
                completedBytes,
                eta,
                ProgressRecordType.Processing);
            UpdateCurrentFileProgress(fileName, "Converting...", ProgressRecordType.Processing);

            var stopwatch = Stopwatch.StartNew();
            var summary = ConvertSingleBonusFile(filePath, inputDirectory);
            stopwatch.Stop();

            _conversionResults.Add(summary);
            if (summary.Success)
            {
                completedBytes += fileSize;
                RecordFileProcessingStats(fileSize, stopwatch.Elapsed);
            }

            percent = totalBytes > 0 ? (int)((completedBytes * 100.0) / totalBytes) : 100;
            UpdateBonusConversionProgress(
                percent,
                currentFileIndex,
                bonusFileCount,
                fileName,
                totalBytes,
                completedBytes,
                null,
                ProgressRecordType.Processing);
            UpdateCurrentFileProgress(fileName, summary.Success ? "Completed" : "Failed", ProgressRecordType.Completed);
        }

        WriteProgress(MediaConversionHelper.CreateSimpleProgressRecord(
            MainActivityId,
            "Bonus file conversion",
            "Completed",
            recordType: ProgressRecordType.Completed));
        WriteProgress(MediaConversionHelper.CreateSimpleProgressRecord(
            CurrentItemActivityId,
            "Current file",
            "Completed",
            recordType: ProgressRecordType.Completed));

        return bonusFileCount;
    }

    private static string FormatByteCount(long bytes)
    {
        if (bytes >= 1 << 30)
            return $"{bytes / (double)(1 << 30):F1} GB";
        if (bytes >= 1 << 20)
            return $"{bytes / (double)(1 << 20):F1} MB";
        if (bytes >= 1 << 10)
            return $"{bytes / (double)(1 << 10):F1} KB";
        return $"{bytes} B";
    }

    private void UpdateBonusConversionProgress(
        int percentComplete,
        int currentFileIndex,
        int totalFiles,
        string currentFileName,
        long totalBytes,
        long completedBytes,
        TimeSpan? eta,
        ProgressRecordType recordType)
    {
        var status = $"File {currentFileIndex} of {totalFiles} ({percentComplete}%)";
        if (totalBytes > 0)
        {
            var completedFormatted = FormatByteCount(completedBytes);
            var totalFormatted = FormatByteCount(totalBytes);
            status += $" — {completedFormatted} / {totalFormatted}";
        }

        status += $" — {currentFileName}";

        var progressRecord = MediaConversionHelper.CreateSimpleProgressRecord(
            MainActivityId,
            "Bonus file conversion",
            status,
            percentComplete,
            recordType: recordType);
        if (eta.HasValue)
            progressRecord.StatusDescription = $"ETA: {FormatTimespan(eta.Value)}";
        WriteProgress(progressRecord);
    }

    private void UpdateCurrentFileProgress(string fileName, string status, ProgressRecordType recordType)
    {
        var progressRecord = MediaConversionHelper.CreateNestedProgressRecord(
            CurrentItemActivityId,
            "Current file",
            status,
            MainActivityId,
            fileName,
            recordType: recordType);
        WriteProgress(progressRecord);
    }

    private static string FormatTimespan(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{time.Hours}h {time.Minutes}m {time.Seconds}s";
        if (time.TotalMinutes >= 1)
            return $"{time.Minutes}m {time.Seconds}s";
        return $"{time.Seconds}s";
    }

    private TimeSpan? CalculateRemainingTime(long completedBytes, long totalBytes)
    {
        if (_fileProcessingStats.Count == 0)
            return null;

        var averageBytesPerSecond = _fileProcessingStats.Average(s => s.BytesPerSecond);
        if (averageBytesPerSecond <= 0)
            return null;

        long remainingBytes = totalBytes - completedBytes;
        if (remainingBytes <= 0)
            return null;

        var remainingSeconds = remainingBytes / averageBytesPerSecond;
        return TimeSpan.FromSeconds(remainingSeconds);
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

            var audioStreams = mediaFile.Streams
                .Where(s => string.Equals(s.Type, "audio", StringComparison.OrdinalIgnoreCase))
                .ToList();

            AudioTrackMapping[] audioMappings;

            if (audioStreams.Count == 0)
            {
                Logger.LogInformation("No audio streams found in bonus file: {InputFilePath}, processing as video-only", inputFilePath);
                audioMappings = Array.Empty<AudioTrackMapping>();
            }
            else
            {
                var englishAudioStreams = audioStreams
                    .Where(s => string.Equals(s.Language, "eng", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var streamsToUse = englishAudioStreams.Count == 0 ? audioStreams : englishAudioStreams;

                try
                {
                    audioMappings = CreateAudioTrackMappings(streamsToUse);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to create audio track mappings for bonus file: {InputFilePath}", inputFilePath);
                    var message = $"Audio settings can't be auto-detected for: {inputFilePath}. Error: {ex.Message}";
                    WriteWarning(message);
                    return new ConversionSummary(inputFilePath, false, message);
                }
            }

            var videoSettings = CreateDefaultVideoEncodingSettings();
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

                var statusMessage = BuildStatusMessage(ex);
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
                var destinationPath = Path.Combine(destFolder, Path.GetFileName(sourceFile));
                try
                {
                    if (File.Exists(destinationPath))
                    {
                        WriteWarning($"Destination file already exists, skipping: {destinationPath}");
                        continue;
                    }

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
                catch (Exception ex)
                {
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
            }
        }

        if (filesMoved == 0)
            WriteWarning($"No bonus content files found to move in source directory {sourceDirectory}");
        else
            WriteVerbose($"{filesMoved} files moved to Plex folders");
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

    private static AudioTrackMapping[] CreateAudioTrackMappings(List<MediaStream> streams)
    {
        var mappings = new List<AudioTrackMapping>();
        var destinationIndex = 0;

        foreach (var stream in streams)
        {
            var channels = AudioTrackMappingService.ParseChannelCount(stream.Raw);
            stream.Tags.TryGetValue("title", out var title);

            AudioTrackMapping mapping;
            var codecLower = stream.Codec.ToLowerInvariant();
            if ((codecLower == "dts" || codecLower == "truehd") &&
                channels >= 6 &&
                !string.Equals(stream.Profile, "dts", StringComparison.OrdinalIgnoreCase))
            {
                mapping = new CopyAudioTrackMapping(
                    title,
                    0,
                    stream.Index - 1,
                    destinationIndex);
            }
            else
            {
                mapping = new EncodeAudioTrackMapping(
                    title,
                    0,
                    stream.Index - 1,
                    destinationIndex,
                    "aac",
                    0,
                    channels);
            }

            mappings.Add(mapping);
            destinationIndex++;
        }

        if (mappings.Count >= 2 &&
            mappings[0] is CopyAudioTrackMapping copyMapping &&
            mappings[1] is EncodeAudioTrackMapping encodeMapping &&
            string.Equals(encodeMapping.DestinationCodec, "aac", StringComparison.OrdinalIgnoreCase) &&
            encodeMapping.DestinationChannels >= 6 &&
            copyMapping.SourceIndex < encodeMapping.SourceIndex)
        {
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

    private VideoEncodingSettings CreateDefaultVideoEncodingSettings()
    {
        var encoder = DefaultVideoEncoder?.Trim();
        var codec = encoder?.ToLowerInvariant() switch
        {
            "nvenc" => "nvenc",
            "x264" => "libx264",
            _ => "libx265"
        };

        if (codec == "nvenc")
        {
            return new NvencVideoEncodingSettings(
                "p5",
                18);
        }

        return new ConstantRateVideoEncodingSettings(
            codec,
            "medium",
            "high",
            "film",
            18,
            VideoEncodingSettings.GetDefaultPixelFormat(codec));
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

