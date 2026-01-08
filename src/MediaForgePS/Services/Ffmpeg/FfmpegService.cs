using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Service for executing Ffmpeg operations.
/// </summary>
public class FfmpegService : IFfmpegService
{
    private const string FFMPEG_EXECUTABLE = "ffmpeg";
    private readonly IExecutableService _executableService;
    private readonly ILogger<FfmpegService> _logger;

    public FfmpegService(IExecutableService executableService, ILogger<FfmpegService> logger)
    {
        _executableService = executableService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ConvertAsync(string inputPath, string outputPath, IEnumerable<string>? arguments = null, CancellationToken cancellationToken = default)
    {
        return await ConvertAsyncInternal(inputPath, outputPath, arguments, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ConvertAsync(string inputPath, string outputPath, IEnumerable<string>? arguments, Action<FfmpegProgress> progressCallback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progressCallback);
        return await ConvertAsyncInternal(inputPath, outputPath, arguments, progressCallback, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ConvertAsyncInternal(string inputPath, string outputPath, IEnumerable<string>? arguments, Action<FfmpegProgress>? progressCallback, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var logMessage = progressCallback != null
            ? "Converting media file from {InputPath} to {OutputPath} with progress tracking"
            : "Converting media file from {InputPath} to {OutputPath}";
        _logger.LogInformation(logMessage, inputPath, outputPath);

        var allArguments = BuildArguments(inputPath, outputPath, arguments, progressCallback != null);
        _logger.LogDebug("FFmpeg arguments: {Arguments}", string.Join(" ", allArguments));

        var result = await ExecuteFfmpegAsync(allArguments, progressCallback, cancellationToken).ConfigureAwait(false);

        HandleResult(result, inputPath, outputPath);
        return true;
    }

    private List<string> BuildArguments(string inputPath, string outputPath, IEnumerable<string>? arguments, bool includeProgress)
    {
        var allArguments = new List<string>();

        if (includeProgress)
        {
            // Suppress normal output when using progress tracking so only progress goes to stdout
            allArguments.Add("-loglevel");
            allArguments.Add("error");
            allArguments.Add("-hide_banner");
            allArguments.Add("-progress");
            allArguments.Add("pipe:1");
        }

        allArguments.Add("-i");
        allArguments.Add(inputPath);

        if (arguments is not null)
        {
            allArguments.AddRange(arguments);
        }

        allArguments.Add("-y"); // Overwrite output file if it exists
        allArguments.Add(outputPath);

        return allArguments;
    }

    private async Task<ExecutableResult> ExecuteFfmpegAsync(List<string> arguments, Action<FfmpegProgress>? progressCallback, CancellationToken cancellationToken)
    {
        if (progressCallback != null)
        {
            var currentProgress = new FfmpegProgress(null, null, null, null, null, null, null, null, null, null);

            return await _executableService.ExecuteAsync(
                FFMPEG_EXECUTABLE,
                arguments,
                line =>
                {
                    var updatedProgress = FfmpegProgressParser.ParseLine(line, currentProgress);
                    if (updatedProgress is not null && updatedProgress != currentProgress)
                    {
                        currentProgress = updatedProgress;
                        try
                        {
                            progressCallback(currentProgress);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Exception in progress callback during FFmpeg conversion");
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return await _executableService.ExecuteAsync(FFMPEG_EXECUTABLE, arguments, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool HandleResult(ExecutableResult result, string inputPath, string outputPath)
    {
        if (result.Exception is not null)
        {
            _logger.LogError(
                result.Exception,
                "Exception occurred during FFmpeg conversion: {InputPath} -> {OutputPath}",
                inputPath,
                outputPath);
            throw new FfmpegConversionException(
                $"Exception occurred during FFmpeg conversion: {result.Exception.Message}",
                inputPath,
                outputPath,
                result.ExitCode,
                result.ErrorOutput,
                result.Exception);
        }

        if (result.ExitCode == 0)
        {
            _logger.LogInformation("FFmpeg conversion successful: {InputPath} -> {OutputPath}", inputPath, outputPath);
            return true;
        }
        else
        {
            var errorMessage = BuildErrorMessage(inputPath, outputPath, result.ExitCode, result.ErrorOutput);
            _logger.LogError(
                "FFmpeg conversion failed: {InputPath} -> {OutputPath}. Exit code: {ExitCode}, Error: {Error}",
                inputPath,
                outputPath,
                result.ExitCode,
                result.ErrorOutput);
            throw new FfmpegConversionException(errorMessage, inputPath, outputPath, result.ExitCode, result.ErrorOutput);
        }
    }

    private static string BuildErrorMessage(string inputPath, string outputPath, int? exitCode, string? errorOutput)
    {
        var message = $"FFmpeg conversion failed: {inputPath} -> {outputPath}";
        if (exitCode.HasValue)
            message += $". Exit code: {exitCode.Value}";
        if (!string.IsNullOrWhiteSpace(errorOutput))
            message += $". Error: {errorOutput.Trim()}";
        return message;
    }
}
