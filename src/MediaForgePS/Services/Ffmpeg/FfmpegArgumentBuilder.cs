using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Builder for constructing Ffmpeg command-line arguments.
/// Supports flags, key-value pairs, and multiple occurrences of the same key.
/// </summary>
public class FfmpegArgumentBuilder(IPlatformService platformService, IArgumentBuilder argumentBuilder)
{
    private readonly IPlatformService _platformService = platformService;
    private readonly IArgumentBuilder _argumentBuilder = argumentBuilder;

    public FfmpegArgumentBuilder AddSourceMap(int sourceStream, char streamType, int sourceIndex)
    {
        _argumentBuilder.AddOption("-map", $"{sourceStream}:{streamType}:{sourceIndex}");
        return this;
    }

    public FfmpegArgumentBuilder AddDestinationCodec(char streamType, string codec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codec);
        _argumentBuilder.AddOption($"-c:{streamType}", codec);
        return this;
    }

    public FfmpegArgumentBuilder AddBitrate(char streamType, int destinationIndex, int bitrate)
    {
        _argumentBuilder.AddOption($"-b:{streamType}:{destinationIndex}", $"{bitrate}k");
        return this;
    }

    public FfmpegArgumentBuilder AddAudioChannels(int destinationIndex, int channels)
    {
        if (channels > 0)
        {
            _argumentBuilder.AddOption($"-ac:a:{destinationIndex}", channels.ToString());
        }
        return this;
    }

    public FfmpegArgumentBuilder AddTitleMetadata(char streamType, int destinationIndex, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return this;

        var quotedTitle = _platformService.QuoteArgument(title);
        _argumentBuilder.AddOption($"-metadata:s:{streamType}:{destinationIndex}", $"title={quotedTitle}");
        return this;
    }

    public FfmpegArgumentBuilder AddPreset(string preset)
    {
        _argumentBuilder.AddOption("-preset", preset);
        return this;
    }

    public FfmpegArgumentBuilder AddCrf(int crf)
    {
        _argumentBuilder.AddOption("-crf", crf.ToString());
        return this;
    }

    public FfmpegArgumentBuilder AddPixelFormat(string pixelFormat = "yuv420p")
    {
        _argumentBuilder.AddOption("-pix_fmt", pixelFormat);
        return this;
    }

    public FfmpegArgumentBuilder AddMapMetadata(int sourceStream)
    {
        _argumentBuilder.AddOption("-map_metadata", sourceStream.ToString());
        return this;
    }

    public FfmpegArgumentBuilder AddMapChapters(int sourceStream)
    {
        _argumentBuilder.AddOption("-map_chapters", sourceStream.ToString());
        return this;
    }

    public FfmpegArgumentBuilder AddMovFlags()
    {
        _argumentBuilder.AddOption("-movflags", "+faststart");
        return this;
    }

    public FfmpegArgumentBuilder AddOption(string key, string value)
    {
        _argumentBuilder.AddOption(key, value);
        return this;
    }

    public IEnumerable<string> ToArguments()
    {
        return _argumentBuilder.ToArguments();
    }
}
