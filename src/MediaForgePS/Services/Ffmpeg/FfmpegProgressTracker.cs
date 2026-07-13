using System.Diagnostics;
using System.Globalization;
using Dadstart.Labs.MediaForge.Parsers;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Parses Ffmpeg <c>-progress</c> key=value lines and reports encode progress.
/// </summary>
public sealed class FfmpegProgressTracker
{
    private readonly TimeSpan _totalDuration;
    private readonly IProgress<FfmpegProgress> _progress;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _outTime;

    public FfmpegProgressTracker(TimeSpan totalDuration, IProgress<FfmpegProgress> progress)
    {
        _totalDuration = totalDuration;
        _progress = progress;
    }

    /// <summary>
    /// Handles a single line of Ffmpeg progress output.
    /// </summary>
    public void HandleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
            return;

        var key = line.AsSpan(0, separatorIndex);
        var value = line[(separatorIndex + 1)..].Trim();

        if (key.Equals("out_time", StringComparison.Ordinal))
        {
            if (TryParseOutTime(value, out var outTime))
                _outTime = outTime;
            return;
        }

        if (key.Equals("out_time_us", StringComparison.Ordinal))
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds) && microseconds >= 0)
                _outTime = TimeSpan.FromTicks(microseconds * 10);
            return;
        }

        if (key.Equals("progress", StringComparison.Ordinal))
            ReportProgress(value.Equals("end", StringComparison.OrdinalIgnoreCase));
    }

    private void ReportProgress(bool isComplete)
    {
        var outTime = isComplete && _totalDuration > TimeSpan.Zero
            ? _totalDuration
            : _outTime;

        if (outTime < TimeSpan.Zero)
            outTime = TimeSpan.Zero;

        if (_totalDuration > TimeSpan.Zero && outTime > _totalDuration)
            outTime = _totalDuration;

        var percentComplete = 0;
        if (_totalDuration > TimeSpan.Zero)
            percentComplete = (int)Math.Clamp(outTime.TotalSeconds / _totalDuration.TotalSeconds * 100.0, 0, 100);
        else if (isComplete)
            percentComplete = 100;

        TimeSpan? estimatedTimeRemaining = null;
        if (isComplete)
        {
            estimatedTimeRemaining = TimeSpan.Zero;
        }
        else if (outTime > TimeSpan.Zero && _stopwatch.Elapsed.TotalSeconds > 0 && _totalDuration > outTime)
        {
            var mediaSecondsPerWallSecond = outTime.TotalSeconds / _stopwatch.Elapsed.TotalSeconds;
            if (mediaSecondsPerWallSecond > 0)
            {
                var remainingMediaSeconds = (_totalDuration - outTime).TotalSeconds;
                estimatedTimeRemaining = TimeSpan.FromSeconds(remainingMediaSeconds / mediaSecondsPerWallSecond);
            }
        }

        _progress.Report(new FfmpegProgress(outTime, _totalDuration, percentComplete, estimatedTimeRemaining));
    }

    private static bool TryParseOutTime(string value, out TimeSpan outTime)
    {
        outTime = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value) || value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            outTime = MediaModelParser.ParseDuration(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
