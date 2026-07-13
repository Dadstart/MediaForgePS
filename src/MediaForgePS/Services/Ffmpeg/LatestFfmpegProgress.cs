namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Thread-safe store for the latest Ffmpeg progress update.
/// </summary>
public sealed class LatestFfmpegProgress : IProgress<FfmpegProgress>
{
    private FfmpegProgress? _latest;

    public FfmpegProgress? Latest => Volatile.Read(ref _latest);

    public void Report(FfmpegProgress value) => Volatile.Write(ref _latest, value);
}
