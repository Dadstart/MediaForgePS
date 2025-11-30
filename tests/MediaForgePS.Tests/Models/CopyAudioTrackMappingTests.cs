using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class CopyAudioTrackMappingTests
{
    [Fact]
    public void ToFfmpegArgs_DefaultsToCopyCodec()
    {
        var mapping = new CopyAudioTrackMapping(null, 0, 1, 2, null);

        var args = mapping.ToFfmpegArgs();

        Assert.Equal("-map", args[0]);
        Assert.Equal("0:a:1", args[1]);
        Assert.Equal("-c:a", args[2]);
        Assert.Equal("copy", args[3]);
    }

    [Fact]
    public void ToFfmpegArgs_UsesProvidedCodec()
    {
        var mapping = new CopyAudioTrackMapping(null, 0, 1, 2, "flac");

        var args = mapping.ToFfmpegArgs();

        Assert.Equal("flac", args[3]);
    }

    [Fact]
    public void ToString_ReflectsDestinationCodec()
    {
        var mapping = new CopyAudioTrackMapping(null, 0, 1, 2, "opus");

        var result = mapping.ToString();

        Assert.Contains("opus", result);
    }
}
