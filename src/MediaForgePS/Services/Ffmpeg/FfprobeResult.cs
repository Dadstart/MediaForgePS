using System.Text.Json;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Result of an ffprobe execution.
/// </summary>
/// <param name="Success">Indicates whether the ffprobe execution was successful.</param>
/// <param name="Json">The JSON output from ffprobe.</param>
public record FfprobeResult(bool Success, string Json);
