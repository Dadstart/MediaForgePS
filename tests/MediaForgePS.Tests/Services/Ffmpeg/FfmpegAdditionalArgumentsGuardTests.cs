using System;
using System.Linq;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfmpegAdditionalArgumentsGuardTests
{
    [Fact]
    public void EnsureSafeForTrustedInput_WhenNull_DoesNotThrow()
        => FfmpegAdditionalArgumentsGuard.EnsureSafeForTrustedInput(null);

    [Fact]
    public void EnsureSafeForTrustedInput_WhenSafeCodecOptions_DoesNotThrow()
        => FfmpegAdditionalArgumentsGuard.EnsureSafeForTrustedInput(["-vf", "scale=1280:720", "-preset", "slow"]);

    [Fact]
    public void EnsureSafeForTrustedInput_WhenExtraInputFlag_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FfmpegAdditionalArgumentsGuard.EnsureSafeForTrustedInput(["-vf", "scale=2", "-i", "other.mkv"]));

        Assert.Equal("additionalArguments", ex.ParamName);
        Assert.Contains("-i", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("file:/etc/passwd")]
    [InlineData("FILE:C:/secret.mkv")]
    [InlineData("file:///tmp/x")]
    public void EnsureSafeForTrustedInput_WhenFileProtocolUrl_ThrowsArgumentException(string argument)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FfmpegAdditionalArgumentsGuard.EnsureSafeForTrustedInput(["-vf", argument]));

        Assert.Equal("additionalArguments", ex.ParamName);
        Assert.Contains("file:", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureSafeForTrustedInput_WhenNullToken_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() =>
            FfmpegAdditionalArgumentsGuard.EnsureSafeForTrustedInput(["-vf", null!]));
}

public class MediaConversionServiceAdditionalArgumentsTests
{
    [Fact]
    public void BuildFfmpegArguments_WithSafeAdditionalArguments_AppendsThem()
    {
        var service = new MediaConversionService(Mock.Of<IFfmpegService>());
        var settings = new ConstantRateVideoEncodingSettings("libx264", "medium", "main", "film", 18, "yuv420p");

        var args = service.BuildFfmpegArguments(settings, [], additionalArguments: ["-vf", "scale=640:360"]).ToArray();

        Assert.Contains("-vf", args);
        Assert.Contains("scale=640:360", args);
    }

    [Fact]
    public void BuildFfmpegArguments_WhenAdditionalArgumentsIncludeInputFlag_Throws()
    {
        var service = new MediaConversionService(Mock.Of<IFfmpegService>());
        var settings = new ConstantRateVideoEncodingSettings("libx264", "medium", "main", "film", 18, "yuv420p");

        var ex = Assert.Throws<ArgumentException>(() =>
            service.BuildFfmpegArguments(settings, [], additionalArguments: ["-i", "sneaky.mkv"]).ToArray());

        Assert.Contains("-i", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFfmpegArguments_WhenAdditionalArgumentsIncludeFileProtocol_Throws()
    {
        var service = new MediaConversionService(Mock.Of<IFfmpegService>());
        var settings = new ConstantRateVideoEncodingSettings("libx264", "medium", "main", "film", 18, "yuv420p");

        var ex = Assert.Throws<ArgumentException>(() =>
            service.BuildFfmpegArguments(settings, [], additionalArguments: ["file:secret.mkv"]).ToArray());

        Assert.Contains("file:", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
