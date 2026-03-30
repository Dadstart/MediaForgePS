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
/// Converts all MKV files in a directory using module conversion services.
/// </summary>
[Cmdlet(VerbsData.Convert, "MkvDirectory")]
[OutputType(typeof(MkvDirectoryConversionResult))]
public class ConvertMkvDirectoryCommand : CmdletBase
{
    private IPathResolver? _pathResolver;
    private IMediaReaderService? _mediaReaderService;
    private IAudioTrackMappingService? _audioTrackMappingService;
    private IMediaConversionService? _mediaConversionService;
    private readonly List<MkvDirectoryConversionResult> _results = new();
    private readonly List<(long FileSizeBytes, TimeSpan ProcessingTime)> _completedFileStats = new();
    private List<(string Path, long Size)>? _sizedMkvFiles;
    private Stopwatch? _batchStopwatch;
    private Stopwatch? _fileStopwatch;
    private int _currentFileIndex;
    private int _batchTotalFiles;
    private long _batchTotalBytes;
    private long _batchCompletedBytes;
    private TimeSpan? _currentFileEstimatedTime;

    /// <summary>
    /// Directory containing MKV files to convert.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        HelpMessage = "Directory containing MKV files to convert")]
    [ValidateNotNullOrEmpty]
    public string InputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Directory where converted files are written. Defaults to InputDirectory.
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 1,
        HelpMessage = "Directory where converted files are written. Defaults to InputDirectory.")]
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Includes MKV files in child directories.
    /// </summary>
    [Parameter(HelpMessage = "Include MKV files in subdirectories.")]
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

    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    private IAudioTrackMappingService AudioTrackMappingService => _audioTrackMappingService ??= ModuleServices.GetRequiredService<IAudioTrackMappingService>();

    private IMediaConversionService MediaConversionService => _mediaConversionService ??= ModuleServices.GetRequiredService<IMediaConversionService>();

    protected override void Process()
    {
        _results.Clear();
        _completedFileStats.Clear();
        _sizedMkvFiles = null;
        _batchCompletedBytes = 0;
        _currentFileIndex = 0;

        if (!TryResolveDirectoryPath(InputDirectory, requireExists: true, out var resolvedInputDirectory))
        {
            WriteError(CreateErrorRecord(
                new DirectoryNotFoundException($"Input directory does not exist: {InputDirectory}"),
                "InputDirectoryNotFound",
                ErrorCategory.ObjectNotFound,
                InputDirectory));
            return;
        }

        var outputDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? resolvedInputDirectory : OutputDirectory!;
        if (!TryResolveDirectoryPath(outputDirectory, requireExists: false, out var resolvedOutputDirectory))
        {
            WriteError(CreateErrorRecord(
                new InvalidOperationException($"Failed to resolve output directory: {outputDirectory}"),
                ErrorIds.OutputPathResolutionFailed,
                ErrorCategory.InvalidArgument,
                outputDirectory));
            return;
        }

        Directory.CreateDirectory(resolvedOutputDirectory);

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var mkvFiles = Directory.EnumerateFiles(resolvedInputDirectory, "*.mkv", searchOption)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (mkvFiles.Length == 0)
        {
            WriteWarning($"No MKV files found in: {resolvedInputDirectory}");
            return;
        }

        var sized = MediaConversionHelper.BuildItemsWithSizes(mkvFiles, static path => path, out var totalBytes);
        _sizedMkvFiles = sized.Select(entry => (entry.Item, entry.Size)).ToList();
        _batchTotalBytes = totalBytes;
        _batchTotalFiles = _sizedMkvFiles.Count;
        _batchStopwatch = Stopwatch.StartNew();

        WriteHostMessage(
            $"Converting {_batchTotalFiles} MKV file(s) (total size: {MediaConversionHelper.FormatByteCount(_batchTotalBytes)})",
            ConsoleColor.Cyan);
        WriteHostMessage($"  Output: {resolvedOutputDirectory}", ConsoleColor.Gray);

        var videoSettings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(DefaultVideoEncoder);
        var additionalArguments = MediaConversionHelper.BuildX265Arguments(X265Params, videoSettings.Codec);

        foreach (var (inputPath, fileSize) in _sizedMkvFiles)
        {
            _currentFileIndex++;
            var fileName = GetFileName(inputPath);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                _currentFileIndex, _batchTotalFiles, fileName, _batchCompletedBytes, _batchTotalBytes);
            var batchEta = CalculateBatchRemainingTime(inputPath, _batchTotalFiles - _currentFileIndex);
            MediaConversionHelper.WriteMainProgress(
                this, "MKV directory conversion", status, percent, batchEta, ProgressRecordType.Processing);

            var result = ConvertSingleFile(
                resolvedInputDirectory,
                resolvedOutputDirectory,
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
        MediaConversionHelper.WriteProgressCompleted(this, "MKV directory conversion", "File conversion");

        var ok = _results.Count(r => r.Success);
        var failed = _results.Count - ok;
        if (failed == 0)
            WriteHostMessage($"Directory conversion finished: {ok} file(s) OK.", ConsoleColor.Green);
        else
            WriteHostMessage($"Directory conversion finished: {ok} succeeded, {failed} failed.", ConsoleColor.Yellow);
    }

    private MkvDirectoryConversionResult ConvertSingleFile(
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
                return new MkvDirectoryConversionResult(inputPath, inputPath, false, "Failed to read media metadata.");
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
                return new MkvDirectoryConversionResult(
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
            return new MkvDirectoryConversionResult(inputPath, resolvedOutputPath, true, "Success");
        }
        catch (FfmpegConversionException ex)
        {
            _fileStopwatch?.Stop();
            var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
            UpdateFileProgress("Conversion failed", fileName, recordType: ProgressRecordType.Completed);
            return new MkvDirectoryConversionResult(inputPath, inputPath, false, statusMessage);
        }
        catch (Exception ex)
        {
            _fileStopwatch?.Stop();
            UpdateFileProgress("Error", fileName, recordType: ProgressRecordType.Completed);
            return new MkvDirectoryConversionResult(inputPath, inputPath, false, ex.Message);
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
                            "MKV directory conversion",
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

        if (_sizedMkvFiles == null)
            return null;

        var remainingPaths = _sizedMkvFiles.Skip(_currentFileIndex).Take(remainingFilesCount);
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
                    "Recorded MKV dir conversion stats: {Bytes} bytes in {Ms} ms",
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
/// Result of converting a single MKV file from a directory batch.
/// </summary>
public record MkvDirectoryConversionResult(string InputPath, string OutputPath, bool Success, string Status);
