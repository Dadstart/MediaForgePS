namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Represents progress information from an Ffmpeg conversion operation.
/// </summary>
/// <param name="Frame">Current frame number.</param>
/// <param name="Fps">Frames per second.</param>
/// <param name="Bitrate">Current bitrate in kbits/s.</param>
/// <param name="TotalSize">Total size of output file in bytes.</param>
/// <param name="OutTimeMs">Output time in milliseconds.</param>
/// <param name="OutTime">Output time as a formatted string (HH:MM:SS.microseconds).</param>
/// <param name="DupFrames">Number of duplicate frames.</param>
/// <param name="DropFrames">Number of dropped frames.</param>
/// <param name="Speed">Encoding speed multiplier (e.g., 1.0x).</param>
/// <param name="Progress">Progress status: "continue" or "end".</param>
public record FfmpegProgress(
    long? Frame,
    double? Fps,
    double? Bitrate,
    long? TotalSize,
    long? OutTimeMs,
    string? OutTime,
    int? DupFrames,
    int? DropFrames,
    double? Speed,
    string? Progress);
