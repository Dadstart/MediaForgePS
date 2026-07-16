using System;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class SubtitleProcessingResultTests
{
    [Fact]
    public void Empty_HasZeroCountsAndEmptyPaths()
    {
        var result = SubtitleProcessingResult.Empty;

        Assert.Equal(0, result.ExtractedCount);
        Assert.Equal(0, result.ConvertedCount);
        Assert.Empty(result.ExtractedPaths);
        Assert.Empty(result.ConvertedPaths);
    }

    [Fact]
    public void Create_UsesPathListCounts()
    {
        var extracted = new[] { @"C:\a.srt", @"C:\a.sup" };
        var converted = new[] { @"C:\a.ocr.srt" };

        var result = SubtitleProcessingResult.Create(extracted, converted);

        Assert.Equal(2, result.ExtractedCount);
        Assert.Equal(1, result.ConvertedCount);
        Assert.Same(extracted, result.ExtractedPaths);
        Assert.Same(converted, result.ConvertedPaths);
    }

    [Fact]
    public void Create_WithNullLists_UsesEmpty()
    {
        var result = SubtitleProcessingResult.Create();

        Assert.Equal(0, result.ExtractedCount);
        Assert.Equal(0, result.ConvertedCount);
        Assert.Empty(result.ExtractedPaths);
        Assert.Empty(result.ConvertedPaths);
    }
}
