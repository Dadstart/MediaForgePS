using System.Globalization;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Parser for Ffmpeg progress output when using -progress pipe:1.
/// </summary>
public static class FfmpegProgressParser
{
    /// <summary>
    /// Parses a line of progress output from Ffmpeg.
    /// </summary>
    /// <param name="line">A line from the progress output (e.g., "frame=123" or "fps=25.5").</param>
    /// <param name="currentProgress">The current progress object to update.</param>
    /// <returns>An updated FfmpegProgress object, or null if the line doesn't contain progress information.</returns>
    public static FfmpegProgress? ParseLine(string line, FfmpegProgress? currentProgress = null)
    {
        if (string.IsNullOrWhiteSpace(line))
            return currentProgress;

        var progress = currentProgress ?? new FfmpegProgress(null, null, null, null, null, null, null, null, null, null);

        var parts = line.Split('=', 2);
        if (parts.Length != 2)
            return progress;

        var key = parts[0].Trim();
        var value = parts[1].Trim();

        return key switch
        {
            "frame" => progress with { Frame = ParseLong(value) },
            "fps" => progress with { Fps = ParseDouble(value) },
            "bitrate" => progress with { Bitrate = ParseBitrate(value) },
            "total_size" => progress with { TotalSize = ParseLong(value) },
            "out_time_ms" => progress with { OutTimeMs = ParseLong(value) },
            "out_time" => progress with { OutTime = value },
            "dup_frames" => progress with { DupFrames = ParseInt(value) },
            "drop_frames" => progress with { DropFrames = ParseInt(value) },
            "speed" => progress with { Speed = ParseSpeed(value) },
            "progress" => progress with { Progress = value },
            _ => progress
        };
    }

    private static long? ParseLong(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static int? ParseInt(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static double? ParseDouble(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static double? ParseBitrate(string value)
    {
        // Bitrate format: "1000.0kbits/s" or "1.5Mbits/s"
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var multiplier = 1.0;

        const string KilobitsPerSecond = "kbits/s";
        const string MegabitsPerSecond = "Mbits/s";
        const string BitsPerSecond = "  bits/s";
        if (trimmed.EndsWith(KilobitsPerSecond, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - KilobitsPerSecond.Length).Trim();
        }
        else if (trimmed.EndsWith(MegabitsPerSecond, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - MegabitsPerSecond.Length).Trim();
            multiplier = 1000.0;
        }
        else if (trimmed.EndsWith(BitsPerSecond, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - BitsPerSecond.Length).Trim();
            multiplier = 0.001;
        }
        else
        {
            // Try parsing as-is
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result * multiplier;

        return null;
    }

    private static double? ParseSpeed(string value)
    {
        // Speed format: "1.0x" or "2.5x"
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(0, trimmed.Length - 1).Trim();

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
    }
}
