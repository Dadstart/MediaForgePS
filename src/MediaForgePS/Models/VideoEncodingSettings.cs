namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Base class for video encoding settings used to configure Ffmpeg video encoding parameters.
/// </summary>
public abstract record VideoEncodingSettings(
    string Codec,
    string Preset,
    string CodecProfile,
    string Tune,
    IList<string> AdditionalArgs)
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
}
