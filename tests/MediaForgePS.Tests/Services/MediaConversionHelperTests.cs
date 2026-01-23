using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class MediaConversionHelperTests
{
    [Theory]
    [InlineData("libx265", true)]
    [InlineData("x265", true)]
    [InlineData("libx264", false)]
    [InlineData("h264", false)]
    public void IsX265Codec_ReturnsExpectedValue(string codec, bool expected)
    {
        var result = MediaConversionHelper.IsX265Codec(codec);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildX265Arguments_WithNoParams_ReturnsNull()
    {
        var result = MediaConversionHelper.BuildX265Arguments(null, "libx265");

        Assert.Null(result);
    }

    [Fact]
    public void BuildX265Arguments_WithX265Params_ReturnsX265Args()
    {
        var result = MediaConversionHelper.BuildX265Arguments("psy-rd=2.0", "libx265");

        var expected = new[] { "-x265-params", "psy-rd=2.0" };
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildX265Arguments_WithNonX265Codec_ReturnsNull()
    {
        var result = MediaConversionHelper.BuildX265Arguments("bframes=8", "libx264");

        Assert.Null(result);
    }
}
