using System;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ocr;

public class UnavailableImageSubtitleOcrConverterTests
{
    [Fact]
    public void IsAvailable_IsFalse()
    {
        var converter = new UnavailableImageSubtitleOcrConverter();
        Assert.False(converter.IsAvailable);
    }

    [Fact]
    public void ExpectedTessDataDescription_MentionsWindowsOnly()
    {
        var converter = new UnavailableImageSubtitleOcrConverter();
        Assert.Contains("Windows", converter.ExpectedTessDataDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConvertToSrt_ThrowsPlatformNotSupportedException()
    {
        var converter = new UnavailableImageSubtitleOcrConverter();
        Assert.Throws<PlatformNotSupportedException>(() =>
            converter.ConvertToSrt("input.sup", "output.srt", TestContext.Current.CancellationToken));
    }
}
