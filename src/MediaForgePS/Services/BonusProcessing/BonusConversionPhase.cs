using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.BonusProcessing;

internal sealed class BonusConversionPhase(
    IMediaReaderService mediaReaderService,
    IMediaConversionService mediaConversionService,
    ILogger logger)
{
    public BonusConversionPhaseResult Run(
        ICmdletIO io,
        BonusConversionRequest request,
        Action<MediaConversionResult>? emitResult,
        CancellationToken cancellationToken)
    {
        var bonusFiles = BonusPlexLayout.GetBonusMkvPaths(request.InputDirectory);
        var bonusFileCount = bonusFiles.Count;

        if (bonusFileCount == 0)
        {
            var suffixList = string.Join(", ", BonusPlexLayout.GetBonusSuffixes());
            io.WriteVerbose($"No bonus-suffix MKV files to convert (suffixes: {suffixList})");
            return new BonusConversionPhaseResult(Array.Empty<MediaConversionResult>(), 0);
        }

        var bonusFilesWithSize = MediaConversionHelper.BuildItemsWithSizes(bonusFiles, static path => path, out var totalBytes)
            .Select(entry => (Path: entry.Item, entry.Size))
            .ToList();

        io.WriteVerbose($"Converting {bonusFileCount} bonus file(s) (total size: {MediaConversionHelper.FormatByteCount(totalBytes)})");

        var results = new List<MediaConversionResult>();
        var fileProcessingStats = new List<(long FileSizeBytes, TimeSpan ProcessingTime)>();
        var batchStopwatch = Stopwatch.StartNew();
        var batchCompletedBytes = 0L;
        var currentFileIndex = 0;

        foreach (var (filePath, fileSize) in bonusFilesWithSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentFileIndex++;
            var fileName = Path.GetFileName(filePath);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                currentFileIndex,
                bonusFileCount,
                fileName,
                batchCompletedBytes,
                totalBytes);
            var remainingBytes = CalculateRemainingBytes(
                bonusFilesWithSize,
                currentFileIndex,
                filePath,
                bonusFileCount - currentFileIndex);
            var eta = remainingBytes.HasValue
                ? MediaConversionHelper.CalculateRemainingTime(remainingBytes.Value, fileProcessingStats)
                : null;

            MediaConversionHelper.WriteMainProgress(io, "Bonus file conversion", status, percent, eta, ProgressRecordType.Processing);

            var summary = ConvertSingleFile(
                io,
                request,
                filePath,
                request.InputDirectory,
                bonusFilesWithSize,
                currentFileIndex,
                bonusFileCount,
                batchCompletedBytes,
                totalBytes,
                fileProcessingStats,
                batchStopwatch,
                cancellationToken);

            results.Add(summary);
            emitResult?.Invoke(summary);

            if (MediaConversionHelper.IsCompletedConversion(summary))
            {
                batchCompletedBytes += fileSize;
                fileProcessingStats.Add((fileSize, summary.ProcessingTime));
            }

            (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
                currentFileIndex,
                bonusFileCount,
                fileName,
                batchCompletedBytes,
                totalBytes);
            MediaConversionHelper.WriteMainProgress(io, "Bonus file conversion", status, percent, null, ProgressRecordType.Processing);
        }

        batchStopwatch.Stop();
        MediaConversionHelper.WriteProgressCompleted(io, "Bonus file conversion", "Current file");

        return new BonusConversionPhaseResult(results, bonusFileCount);
    }

    private MediaConversionResult ConvertSingleFile(
        ICmdletIO io,
        BonusConversionRequest request,
        string inputFilePath,
        string inputDirectory,
        IReadOnlyList<(string Path, long Size)> sizedBonusFiles,
        int currentFileIndex,
        int totalFiles,
        long batchCompletedBytes,
        long batchTotalBytes,
        List<(long FileSizeBytes, TimeSpan ProcessingTime)> fileProcessingStats,
        Stopwatch batchStopwatch,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(inputFilePath);
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Processing bonus file: {InputFilePath}", inputFilePath);
        io.WriteVerbose($"Processing bonus file: {inputFilePath}");
        WriteCurrentItemProgress(io, $"Preparing {fileName}", fileName, percentComplete: 0);

        try
        {
            WriteCurrentItemProgress(io, "Reading media metadata", fileName);
            var mediaFile = mediaReaderService.GetMediaFileAsync(inputFilePath, cancellationToken)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (mediaFile == null)
            {
                const string StatusMessage = "Failed to read media file information";
                io.WriteWarning($"{StatusMessage}: {inputFilePath}");
                stopwatch.Stop();
                WriteCurrentItemProgress(io, StatusMessage, fileName, recordType: ProgressRecordType.Completed);
                return MediaConversionHelper.CreateConversionResult(
                    inputFilePath, inputFilePath, false, StatusMessage, stopwatch.Elapsed);
            }

            WriteCurrentItemProgress(io, "Building audio track mappings", fileName);
            AudioTrackMapping[] audioMappings;
            var audioSelection = MediaConversionHelper.SelectPreferredAudioStreams(mediaFile.Streams);
            if (audioSelection.TotalAudioStreamCount == 0)
            {
                logger.LogInformation("No audio streams found in bonus file: {InputFilePath}, processing as video-only", inputFilePath);
                audioMappings = Array.Empty<AudioTrackMapping>();
            }
            else
            {
                if (audioSelection.EnglishAudioStreamCount == 0)
                    logger.LogInformation("No English audio streams found in bonus file: {InputFilePath}, using all audio streams", inputFilePath);

                try
                {
                    audioMappings = MediaConversionHelper.CreateAutomaticAudioTrackMappings(audioSelection.SelectedStreams);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create audio track mappings for bonus file: {InputFilePath}", inputFilePath);
                    var message = $"Audio settings can't be auto-detected for: {inputFilePath}. Error: {ex.Message}";
                    io.WriteWarning(message);
                    stopwatch.Stop();
                    WriteCurrentItemProgress(io, "Failed to build audio mappings", fileName, recordType: ProgressRecordType.Completed);
                    return MediaConversionHelper.CreateConversionResult(
                        inputFilePath, inputFilePath, false, message, stopwatch.Elapsed);
                }
            }

            var videoSettings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(request.DefaultVideoEncoder);
            var x265Arguments = MediaConversionHelper.BuildX265Arguments(null, videoSettings.Codec);

            var outputFileName = Path.GetFileNameWithoutExtension(inputFilePath) + ".mp4";
            var outputFilePath = Path.Combine(inputDirectory, outputFileName);

            if (!TryEnsureOutputCanBeWritten(io, outputFilePath, request.Force))
            {
                stopwatch.Stop();
                WriteCurrentItemProgress(io, "Skipped (output exists)", fileName, recordType: ProgressRecordType.Completed);
                return MediaConversionHelper.CreateConversionResult(
                    inputFilePath,
                    outputFilePath,
                    false,
                    "Output file already exists. Use -Force to overwrite.",
                    stopwatch.Elapsed);
            }

            try
            {
                RunConversionWithProgress(
                    io,
                    request,
                    inputFilePath,
                    outputFilePath,
                    videoSettings,
                    audioMappings,
                    x265Arguments,
                    outputFileName,
                    MediaConversionHelper.GetTotalDuration(mediaFile),
                    sizedBonusFiles,
                    currentFileIndex,
                    totalFiles,
                    batchCompletedBytes,
                    batchTotalBytes,
                    fileProcessingStats,
                    batchStopwatch,
                    cancellationToken);

                stopwatch.Stop();
                WriteCurrentItemProgress(io, "Conversion completed", fileName, recordType: ProgressRecordType.Completed);
                return MediaConversionHelper.CreateConversionResult(
                    inputFilePath, outputFilePath, true, MediaConversionResult.CompletedStatus, stopwatch.Elapsed);
            }
            catch (FfmpegConversionException ex)
            {
                stopwatch.Stop();
                var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
                WriteCurrentItemProgress(io, "Conversion failed", fileName, recordType: ProgressRecordType.Completed);
                return MediaConversionHelper.CreateConversionResult(
                    inputFilePath, outputFilePath, false, statusMessage, stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                WriteCurrentItemProgress(io, "Error", fileName, recordType: ProgressRecordType.Completed);
                return MediaConversionHelper.CreateConversionResult(
                    inputFilePath, outputFilePath, false, $"Conversion failed: {ex.Message}", stopwatch.Elapsed);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed to read media file for bonus processing: {InputFilePath}", inputFilePath);
            var message = $"Failed to read media file: {ex.Message}";
            io.WriteWarning($"{message} ({inputFilePath})");
            WriteCurrentItemProgress(io, "Error", fileName, recordType: ProgressRecordType.Completed);
            return MediaConversionHelper.CreateConversionResult(
                inputFilePath, inputFilePath, false, message, stopwatch.Elapsed);
        }
    }

    private void RunConversionWithProgress(
        ICmdletIO io,
        BonusConversionRequest request,
        string inputFilePath,
        string outputFilePath,
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        string[]? additionalArguments,
        string outputFileName,
        TimeSpan? totalDuration,
        IReadOnlyList<(string Path, long Size)> sizedBonusFiles,
        int currentFileIndex,
        int totalFiles,
        long batchCompletedBytes,
        long batchTotalBytes,
        List<(long FileSizeBytes, TimeSpan ProcessingTime)> fileProcessingStats,
        Stopwatch batchStopwatch,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Starting bonus media file conversion: {InputFilePath} -> {OutputFilePath}",
                inputFilePath,
                outputFilePath);

            var encodeStatus = $"Encoding to {videoSettings.Codec} ({videoSettings.Preset} preset)";
            var encodeStartElapsed = batchStopwatch.Elapsed;

            TimeSpan? initialBatchEta = null;
            if (currentFileIndex <= totalFiles)
            {
                var remainingBytes = CalculateRemainingBytes(
                    sizedBonusFiles,
                    currentFileIndex,
                    inputFilePath,
                    totalFiles - currentFileIndex);
                if (remainingBytes.HasValue)
                    initialBatchEta = MediaConversionHelper.CalculateRemainingTime(remainingBytes.Value, fileProcessingStats);
            }

            Action? reportBatchProgress = null;
            if (initialBatchEta.HasValue)
            {
                var batchEta = initialBatchEta.Value;
                reportBatchProgress = () =>
                {
                    var remaining = batchEta - (batchStopwatch.Elapsed - encodeStartElapsed);
                    if (remaining.TotalSeconds <= 0)
                        return;

                    var (batchStatus, batchPercent) = MediaConversionHelper.BuildBatchProgressStatus(
                        currentFileIndex,
                        totalFiles,
                        Path.GetFileName(inputFilePath),
                        batchCompletedBytes,
                        batchTotalBytes);
                    MediaConversionHelper.WriteMainProgress(
                        io,
                        "Bonus file conversion",
                        batchStatus,
                        batchPercent,
                        remaining,
                        ProgressRecordType.Processing);
                };
            }

            MediaConversionHelper.RunConversionWithProgress(
                (progress, token) => mediaConversionService.ExecuteConversion(
                    inputFilePath,
                    outputFilePath,
                    videoSettings,
                    audioMappings,
                    additionalArguments,
                    progress,
                    token,
                    overwrite: request.Force,
                    totalDuration: totalDuration),
                encodeStatus,
                outputFileName,
                update => WriteCurrentItemProgress(
                    io,
                    update.Status,
                    update.CurrentOperation,
                    update.PercentComplete,
                    eta: update.Eta),
                cancellationToken,
                reportBatchProgress);

            logger.LogInformation(
                "Successfully converted bonus media file: {InputFilePath} -> {OutputFilePath}",
                inputFilePath,
                outputFilePath);
        }
        catch (FfmpegConversionException ex)
        {
            logger.LogError(
                ex,
                "FFmpeg conversion failed for bonus media file: {InputFilePath} -> {OutputFilePath}",
                inputFilePath,
                outputFilePath);
            var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
            WriteCurrentItemProgress(io, statusMessage, outputFileName, recordType: ProgressRecordType.Completed);
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Exception occurred while converting bonus media file: {InputFilePath} -> {OutputFilePath}",
                inputFilePath,
                outputFilePath);
            WriteCurrentItemProgress(io, ex.Message, outputFileName, recordType: ProgressRecordType.Completed);
            throw;
        }
    }

    private static long? CalculateRemainingBytes(
        IReadOnlyList<(string Path, long Size)> sizedFiles,
        int currentFileIndex,
        string currentFilePath,
        int remainingFilesCount)
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

        var remainingPaths = sizedFiles.Skip(currentFileIndex).Take(remainingFilesCount);
        foreach (var entry in remainingPaths)
            remainingBytes += entry.Size;

        return remainingBytes;
    }

    private static bool TryEnsureOutputCanBeWritten(ICmdletIO io, string resolvedOutputPath, bool force)
    {
        if (!File.Exists(resolvedOutputPath))
            return true;

        if (force)
            return true;

        io.WriteError(new ErrorRecord(
            new IOException($"Output file already exists: {resolvedOutputPath}. Use -Force to overwrite."),
            "OutputFileExists",
            ErrorCategory.ResourceExists,
            resolvedOutputPath));
        return false;
    }

    private static void WriteCurrentItemProgress(
        ICmdletIO io,
        string status,
        string? currentOperation = null,
        int? percentComplete = null,
        ProgressRecordType recordType = ProgressRecordType.Processing,
        TimeSpan? eta = null) =>
        MediaConversionHelper.WriteCurrentItemProgress(
            io,
            "Current file",
            status,
            currentOperation,
            percentComplete,
            eta,
            recordType);
}
