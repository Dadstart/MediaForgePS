namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Default process timeouts for external tools (FFmpeg, Ffprobe, mkvextract).
/// Callers link these with <see cref="System.Threading.CancellationToken"/> (e.g. StoppingToken)
/// via <see cref="IExecutableService.ExecuteAsync"/>.
/// </summary>
public static class ProcessTimeouts
{
    /// <summary>
    /// Short timeout for metadata probes (Ffprobe).
    /// </summary>
    public static readonly TimeSpan Probe = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Timeout for stream extraction and chapter splits (typically stream-copy, no re-encode).
    /// </summary>
    public static readonly TimeSpan Extract = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Long timeout for re-encoding conversions.
    /// </summary>
    public static readonly TimeSpan Encode = TimeSpan.FromHours(12);
}
