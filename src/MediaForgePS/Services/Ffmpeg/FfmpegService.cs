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
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        _logger.LogInformation("Converting media file from {InputPath} to {OutputPath}", inputPath, outputPath);

        var allArguments = BuildArguments(inputPath, outputPath, arguments);
        _logger.LogDebug("FFmpeg arguments: {Arguments}", string.Join(" ", allArguments));

        var result = await _executableService.ExecuteAsync(FFMPEG_EXECUTABLE, allArguments, cancellationToken).ConfigureAwait(false);

        HandleResult(result, inputPath, outputPath);
        return true;
    }

    private List<string> BuildArguments(string inputPath, string outputPath, IEnumerable<string>? arguments)
    {
        var allArguments = new List<string>();

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
