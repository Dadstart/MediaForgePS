using System.Text;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Builder for constructing Ffmpeg command-line arguments.
/// Supports flags, key-value pairs, and multiple occurrences of the same key.
/// </summary>
public class FfmpegArgumentBuilder
{
    private readonly IArgumentBuilder _argumentBuilder = new ArgumentBuilder();

    /// <summary>
    /// Adds a source map argument to select a specific stream from the input.
    /// </summary>
    /// <param name="sourceStream">The source stream index.</param>
    /// <param name="streamType">The stream type ('v' for video, 'a' for audio).</param>
    /// <param name="sourceIndex">The source index within the stream type.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddSourceMap(int sourceStream, char streamType, int sourceIndex)
    {
        _argumentBuilder.AddOption("-map", $"{sourceStream}:{streamType}:{sourceIndex}");
        return this;
    }

    /// <summary>
    /// Adds a destination codec argument.
    /// </summary>
    /// <param name="streamType">The stream type ('v' for video, 'a' for audio).</param>
    /// <param name="codec">The codec name (e.g., "libx264", "copy").</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddDestinationCodec(char streamType, string codec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codec);
        _argumentBuilder.AddOption($"-c:{streamType}", codec);
        return this;
    }

    /// <summary>
    /// Adds a bitrate argument for the specified stream.
    /// </summary>
    /// <param name="streamType">The stream type ('v' for video, 'a' for audio).</param>
    /// <param name="destinationIndex">The destination stream index.</param>
    /// <param name="bitrate">The bitrate in kilobits per second.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddBitrate(char streamType, int destinationIndex, int bitrate)
    {
        _argumentBuilder.AddOption($"-b:{streamType}:{destinationIndex}", $"{bitrate}k");
        return this;
    }

    /// <summary>
    /// Adds an audio channel count argument.
    /// </summary>
    /// <param name="destinationIndex">The destination stream index.</param>
    /// <param name="channels">The number of audio channels (only added if greater than 0).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddAudioChannels(int destinationIndex, int channels)
    {
        if (channels > 0)
            _argumentBuilder.AddOption($"-ac:{destinationIndex}", channels.ToString());
        return this;
    }

    /// <summary>
    /// Adds title metadata for the specified stream.
    /// </summary>
    /// <param name="streamType">The stream type ('v' for video, 'a' for audio).</param>
    /// <param name="destinationIndex">The destination stream index.</param>
    /// <param name="title">The title to set (only added if not null or whitespace).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddTitleMetadata(char streamType, int destinationIndex, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return this;

        _argumentBuilder.AddOption(
            $"-metadata:s:{streamType}:{destinationIndex}",
            $"title={EscapeMetadataValue(title)}");
        return this;
    }

    /// <summary>
    /// Escapes a metadata value per FFmpeg metadata rules.
    /// Characters <c>=</c>, <c>;</c>, <c>#</c>, <c>\</c>, and newline are prefixed with a backslash.
    /// </summary>
    /// <param name="value">Unescaped metadata value.</param>
    /// <returns>Escaped value safe for FFmpeg metadata key=value forms.</returns>
    internal static string EscapeMetadataValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var needsEscape = false;
        foreach (var c in value)
        {
            if (IsMetadataSpecialCharacter(c))
            {
                needsEscape = true;
                break;
            }
        }

        if (!needsEscape)
            return value;

        var builder = new StringBuilder(value.Length * 2);
        foreach (var c in value)
        {
            if (IsMetadataSpecialCharacter(c))
                builder.Append('\\');
            builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool IsMetadataSpecialCharacter(char c) =>
        c is '=' or ';' or '#' or '\\' or '\n';

    /// <summary>
    /// Adds a preset argument for encoding.
    /// </summary>
    /// <param name="preset">The preset name (e.g., "slow", "medium", "fast").</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddPreset(string preset)
    {
        _argumentBuilder.AddOption("-preset", preset);
        return this;
    }

    /// <summary>
    /// Adds a CRF (Constant Rate Factor) argument for quality-based encoding.
    /// </summary>
    /// <param name="crf">The CRF value.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddCrf(int crf)
    {
        _argumentBuilder.AddOption("-crf", crf.ToString());
        return this;
    }

    /// <summary>
    /// Adds a CQ (Constant Quality) argument for NVENC quality-based encoding.
    /// </summary>
    /// <param name="cq">The CQ value (0-51, lower is better quality).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddCq(int cq)
    {
        _argumentBuilder.AddOption("-cq", cq.ToString());
        return this;
    }

    /// <summary>
    /// Adds a pixel format argument.
    /// </summary>
    /// <param name="pixelFormat">The pixel format (default: "yuv420p").</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddPixelFormat(string pixelFormat = "yuv420p")
    {
        _argumentBuilder.AddOption("-pix_fmt", pixelFormat);
        return this;
    }

    /// <summary>
    /// Adds a metadata mapping argument to copy metadata from the source stream.
    /// </summary>
    /// <param name="sourceStream">The source stream index to copy metadata from.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddMapMetadata(int sourceStream)
    {
        _argumentBuilder.AddOption("-map_metadata", sourceStream.ToString());
        return this;
    }

    /// <summary>
    /// Adds a chapter mapping argument to copy chapters from the source stream.
    /// </summary>
    /// <param name="sourceStream">The source stream index to copy chapters from.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddMapChapters(int sourceStream)
    {
        _argumentBuilder.AddOption("-map_chapters", sourceStream.ToString());
        return this;
    }

    /// <summary>
    /// Adds MOV flags to enable fast start for web streaming.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddMovFlags()
    {
        _argumentBuilder.AddOption("-movflags", "+faststart");
        return this;
    }

    /// <summary>
    /// Adds two-pass encoding arguments (<c>-pass</c> and <c>-passlogfile</c>).
    /// </summary>
    /// <param name="pass">Pass number (1 or 2).</param>
    /// <param name="passLogFile">Pass logfile path without extension suffix FFmpeg may append.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddPass(int pass, string passLogFile)
    {
        if (pass is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(pass), pass, "Pass must be 1 or 2.");

        ArgumentException.ThrowIfNullOrWhiteSpace(passLogFile);
        _argumentBuilder.AddOption("-pass", pass.ToString());
        _argumentBuilder.AddOption("-passlogfile", passLogFile);
        return this;
    }

    /// <summary>
    /// Adds a bare flag argument (e.g. <c>-an</c>).
    /// </summary>
    public FfmpegArgumentBuilder AddFlag(string flag)
    {
        _argumentBuilder.AddFlag(flag);
        return this;
    }

    /// <summary>
    /// Adds a custom option with a key-value pair.
    /// </summary>
    /// <param name="key">The option key (e.g., "-filter:v").</param>
    /// <param name="value">The option value.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FfmpegArgumentBuilder AddOption(string key, string value)
    {
        _argumentBuilder.AddOption(key, value);
        return this;
    }

    /// <summary>
    /// Converts the builder's arguments to an enumerable of strings suitable for Ffmpeg command-line execution.
    /// </summary>
    /// <returns>An enumerable of argument strings.</returns>
    public IEnumerable<string> ToArguments()
    {
        return _argumentBuilder.ToArguments();
    }
}
