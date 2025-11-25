using System.Text.Json;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

public record FfprobeResult(bool Success, string Json);