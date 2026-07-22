using System;
using System.Linq;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class ConstantRateVideoEncodingSettingsTests
{
    [Fact]
    public void ToFfmpegArgs_IncludesProfileAndTune()
    {
        var settings = new ConstantRateVideoEncodingSettings(
            "libx264",
            "slow",
            "high",
            "film",
            18,
            "yuv420p");

        var args = settings.ToFfmpegArgs(null).ToArray();

        var profileIndex = Array.IndexOf(args, "-profile:v");
        Assert.True(profileIndex >= 0);
        Assert.Equal("high", args[profileIndex + 1]);

        var tuneIndex = Array.IndexOf(args, "-tune");
        Assert.True(tuneIndex >= 0);
        Assert.Equal("film", args[tuneIndex + 1]);

        Assert.Contains("-crf", args);
        Assert.Contains("18", args);
    }
}
