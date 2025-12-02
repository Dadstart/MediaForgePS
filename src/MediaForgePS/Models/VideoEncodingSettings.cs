namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Base class for video encoding settings used to configure Ffmpeg video encoding parameters.
/// </summary>
public abstract record VideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune)
{
    /// <summary>
    /// Indicates whether the encoding uses a single pass (true) or two-pass encoding (false).
    /// </summary>
    public abstract bool IsSinglePass { get; }

    /// <summary>
    /// Returns a string representation of the encoding settings.
    /// </summary>
    public abstract override string ToString();

    /// <summary>
    /// Converts the encoding settings to a list of Ffmpeg arguments for the specified pass.
    /// </summary>
    /// <param name="pass">The encoding pass number (1 or 2 for two-pass, null for single-pass).</param>
    /// <returns>A list of Ffmpeg arguments.</returns>
    public abstract IList<string> ToFfmpegArgs(int? pass);

    /// <summary>
    /// Converts the codec name to the Ffmpeg codec name (e.g., "x264" to "libx264").
    /// </summary>
    /// <returns>The Ffmpeg codec name.</returns>
    protected string GetFfmpegCodecName() => Codec == "x264" ? "libx264" : Codec;

    /// <summary>
    /// Adds video stream mapping arguments to the Ffmpeg argument list.
    /// </summary>
    /// <param name="args">The list of Ffmpeg arguments to append to.</param>
    protected void AddVideoStreamMap(IList<string> args)
    {
        args.Add("-map");
        args.Add("0:v:0");
    }

    /// <summary>
    /// Adds video codec arguments to the Ffmpeg argument list.
    /// </summary>
    /// <param name="args">The list of Ffmpeg arguments to append to.</param>
    protected void AddVideoCodec(IList<string> args)
    {
        args.Add("-c:v");
        args.Add(GetFfmpegCodecName());
    }

    /// <summary>
    /// Adds preset arguments to the Ffmpeg argument list.
    /// </summary>
    /// <param name="args">The list of Ffmpeg arguments to append to.</param>
    protected void AddPreset(IList<string> args)
    {
        args.Add("-preset");
        args.Add(Preset);
    }

    /// <summary>
    /// Adds metadata, chapters, and movflags arguments to the Ffmpeg argument list.
    /// </summary>
    /// <param name="args">The list of Ffmpeg arguments to append to.</param>
    protected void AddMetadataChaptersAndMovflags(IList<string> args)
    {
        args.Add("-map_metadata");
        args.Add("0");
        args.Add("-map_chapters");
        args.Add("0");
        args.Add("-movflags");
        args.Add("+faststart");
    }
}
