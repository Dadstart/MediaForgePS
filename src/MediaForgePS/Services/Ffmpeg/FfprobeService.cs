using System;
using System.Text.Json;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Service for executing ffprobe.
/// </summary>
/// <param name="executableService">The executable service to use. This is used to execute the ffprobe command.</param>
public class FfprobeService(IExecutableService executableService) : IFfprobeService
{
    private const string FFPROBE_EXECUTABLE = "ffprobe";
    private readonly IExecutableService _executableService = executableService;

    /// <inheritdoc />
    public async Task<FfprobeResult> Execute(string path, IEnumerable<string> arguments)
    {
        // TODO: check if ffmpeg/ffprobe is installed
        string[] additionalArguments = ["-v", "error", "-of", "json"];
        var pathArgument = new[] { "-i", path };
        var allArguments = additionalArguments.Concat(arguments ?? Enumerable.Empty<string>()).Concat(pathArgument);
        var result = await _executableService.Execute(FFPROBE_EXECUTABLE, allArguments);
        return new FfprobeResult(result.ExitCode == 0, result.Output ?? string.Empty);
    }
}