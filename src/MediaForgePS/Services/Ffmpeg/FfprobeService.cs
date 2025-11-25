using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Service for executing ffprobe.
/// </summary>
public class FfprobeService : IFfprobeService
{
    private const string FFPROBE_EXECUTABLE = "ffprobe";
    private readonly IExecutableService _executableService;
    private readonly ILogger<FfprobeService> _logger;

    public FfprobeService(IExecutableService executableService, ILogger<FfprobeService> logger)
    {
        _executableService = executableService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FfprobeResult> Execute(string path, IEnumerable<string> arguments)
    {
        _logger.LogInformation("Executing ffprobe for media file: {Path}", path);

        // TODO: check if ffmpeg/ffprobe is installed
        string[] additionalArguments = ["-v", "error", "-of", "json"];
        var pathArgument = new[] { "-i", path };
        var allArguments = additionalArguments.Concat(arguments ?? Enumerable.Empty<string>()).Concat(pathArgument);

        _logger.LogDebug("FFprobe arguments: {Arguments}", string.Join(" ", allArguments));

        var result = await _executableService.Execute(FFPROBE_EXECUTABLE, allArguments);

        if (result.ExitCode == 0)
        {
            _logger.LogInformation("FFprobe execution successful for: {Path}", path);
            _logger.LogTrace("FFprobe output length: {OutputLength} characters", result.Output?.Length ?? 0);
        }
        else
        {
            _logger.LogWarning(
                "FFprobe execution failed for: {Path}. Exit code: {ExitCode}, Error: {Error}",
                path,
                result.ExitCode,
                result.ErrorOutput);
        }

        return new FfprobeResult(result.ExitCode == 0, result.Output ?? string.Empty);
    }
}