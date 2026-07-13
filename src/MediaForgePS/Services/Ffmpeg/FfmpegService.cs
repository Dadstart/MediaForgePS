using System.Globalization;
using System.Text.Json;
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
    private readonly IFfprobeService _ffprobeService;
    private readonly ILogger<FfmpegService> _logger;

    public FfmpegService(
        IExecutableService executableService,
        IFfprobeService ffprobeService,
        ILogger<FfmpegService> logger)
    {
        _executableService = executableService;
        _ffprobeService = ffprobeService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ConvertAsync(
        string inputPath,
        string outputPath,
        IEnumerable<string>? arguments = null,
        IProgress<FfmpegProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        _logger.LogInformation("Converting media file from {InputPath} to {OutputPath}", inputPath, outputPath);

        var totalDuration = progress is null
            ? TimeSpan.Zero
            : await GetDurationAsync(inputPath, cancellationToken).ConfigureAwait(false);

        var allArguments = BuildArguments(inputPath, outputPath, arguments, trackProgress: progress is not null);
        _logger.LogDebug("FFmpeg arguments: {Arguments}", string.Join(" ", allArguments));

        ExecutableResult result;
        if (progress is null)
        {
            result = await _executableService.ExecuteAsync(FFMPEG_EXECUTABLE, allArguments, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var tracker = new FfmpegProgressTracker(totalDuration, progress);
            result = await _executableService.ExecuteAsync(FFMPEG_EXECUTABLE, allArguments, tracker.HandleLine, cancellationToken).ConfigureAwait(false);
        }

        HandleResult(result, inputPath, outputPath);
        return true;
    }

    private async Task<TimeSpan> GetDurationAsync(string inputPath, CancellationToken cancellationToken)
    {
        var probeResult = await _ffprobeService.ExecuteAsync(
            inputPath,
            ["-show_entries", "format=duration"],
            cancellationToken).ConfigureAwait(false);

        if (!probeResult.Success || string.IsNullOrWhiteSpace(probeResult.Json))
        {
            _logger.LogWarning("Unable to determine media duration via Ffprobe for {InputPath}; progress percent may be unavailable", inputPath);
            return TimeSpan.Zero;
        }

        try
        {
            using var document = JsonDocument.Parse(probeResult.Json);
            if (!document.RootElement.TryGetProperty("format", out var formatElement))
                return TimeSpan.Zero;

            if (!formatElement.TryGetProperty("duration", out var durationElement))
                return TimeSpan.Zero;

            var durationSeconds = durationElement.ValueKind switch
            {
                JsonValueKind.Number => durationElement.GetDouble(),
                JsonValueKind.String when double.TryParse(
                    durationElement.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => double.NaN
            };

            if (double.IsNaN(durationSeconds) || durationSeconds <= 0)
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds(durationSeconds);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Ffprobe duration JSON for {InputPath}", inputPath);
            return TimeSpan.Zero;
        }
    }

    private List<string> BuildArguments(string inputPath, string outputPath, IEnumerable<string>? arguments, bool trackProgress)
    {
        var allArguments = new List<string>();

        if (trackProgress)
        {
            allArguments.Add("-nostats");
            allArguments.Add("-progress");
            allArguments.Add("pipe:1");
        }

        allArguments.Add("-i");
        allArguments.Add(inputPath);

        if (arguments is not null)
            allArguments.AddRange(arguments);

        allArguments.Add("-y"); // Overwrite output file if it exists
        allArguments.Add(outputPath);

        return allArguments;
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
