using System.Linq;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class VariableRateVideoEncodingSettingsTests
{
    [Fact]
    public void ToFfmpegArgs_Pass1_IncludesPassLogAndDisablesAudio()
    {
        var settings = new VariableRateVideoEncodingSettings(
            "libx264",
            "medium",
            "main",
            "film",
            2500,
            "yuv420p");

        var args = settings.ToFfmpegArgs(1, @"C:\temp\passlog").ToArray();

        Assert.Contains("-map", args);
        Assert.Contains("0:v:0", args);
        Assert.Contains("-pass", args);
        Assert.Contains("1", args);
        Assert.Contains("-passlogfile", args);
        Assert.Contains(@"C:\temp\passlog", args);
        Assert.Contains("-an", args);
        Assert.DoesNotContain("-map_metadata", args);
        Assert.DoesNotContain("-movflags", args);
    }

    [Fact]
    public void ToFfmpegArgs_Pass2_IncludesPass2MetadataAndMovFlags()
    {
        var settings = new VariableRateVideoEncodingSettings(
            "libx264",
            "medium",
            "main",
            "film",
            2500,
            "yuv420p");

        var args = settings.ToFfmpegArgs(2, @"C:\temp\passlog").ToArray();

        Assert.Contains("-pass", args);
        Assert.Contains("2", args);
        Assert.Contains("-passlogfile", args);
        Assert.Contains("-map_metadata", args);
        Assert.Contains("-map_chapters", args);
        Assert.Contains("-movflags", args);
        Assert.DoesNotContain("-an", args);
    }
}
