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
    public async Task<bool> ConvertAsync(string inputPath, string outputPath, IEnumerable<string>? arguments = null)
    {
        _logger.LogInformation("Converting media file from {InputPath} to {OutputPath}", inputPath, outputPath);

        // Build ffmpeg arguments: -i input, optional custom arguments, output
        var allArguments = new List<string> { "-i", inputPath };

        if (arguments != null)
        {
            allArguments.AddRange(arguments);
        }

        allArguments.Add("-y"); // Overwrite output file if it exists
        allArguments.Add(outputPath);

        _logger.LogDebug("FFmpeg arguments: {Arguments}", string.Join(" ", allArguments));

        var result = await _executableService.Execute(FFMPEG_EXECUTABLE, allArguments).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            _logger.LogInformation("FFmpeg conversion successful: {InputPath} -> {OutputPath}", inputPath, outputPath);
            return true;
        }
        else
        {
            _logger.LogError(
                "FFmpeg conversion failed: {InputPath} -> {OutputPath}. Exit code: {ExitCode}, Error: {Error}",
                inputPath,
                outputPath,
                result.ExitCode,
                result.ErrorOutput);
            return false;
        }
    }
}
