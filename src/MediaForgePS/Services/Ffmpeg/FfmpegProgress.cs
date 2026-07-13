namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Progress information reported during an Ffmpeg conversion.
/// </summary>
/// <param name="OutTime">Current output media timestamp from Ffmpeg <c>out_time</c>.</param>
/// <param name="TotalDuration">Total media duration from Ffprobe.</param>
/// <param name="PercentComplete">Completion percentage (0-100) based on <paramref name="OutTime"/> / <paramref name="TotalDuration"/>.</param>
/// <param name="EstimatedTimeRemaining">Estimated wall-clock time remaining, or null when not yet estimable.</param>
public record FfmpegProgress(
    TimeSpan OutTime,
    TimeSpan TotalDuration,
    int PercentComplete,
    TimeSpan? EstimatedTimeRemaining);
