using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class ChapterRangeHelperTests
{
    [Fact]
    public void NormalizeChapterRanges_WithChapterRangeObjects_ReturnsExpectedValues()
    {
        var result = ChapterRangeHelper.NormalizeChapterRanges(
        [
            new ChapterRange(1, 2, "Episode 1"),
            new ChapterRange(3, 4)
        ]);

        Assert.Equal(2, result.Count);
        Assert.Equal((1, 2, "Episode 1"), result[0]);
        Assert.Equal((3, 4, (string?)null), result[1]);
    }

    [Fact]
    public void NormalizeChapterRanges_WithPsObjectProperties_ReturnsExpectedValues()
    {
        var psObject = new PSObject();
        psObject.Properties.Add(new PSNoteProperty("Start", 5));
        psObject.Properties.Add(new PSNoteProperty("End", 6));
        psObject.Properties.Add(new PSNoteProperty("OutputName", "Episode 3"));

        var result = ChapterRangeHelper.NormalizeChapterRanges([psObject]);

        Assert.Single(result);
        Assert.Equal((5, 6, "Episode 3"), result[0]);
    }

    [Fact]
    public void NormalizeChapterRanges_WhenStartOrEndMissing_ThrowsArgumentException()
    {
        var psObject = new PSObject();
        psObject.Properties.Add(new PSNoteProperty("Start", 1));

        var exception = Assert.Throws<ArgumentException>(() => ChapterRangeHelper.NormalizeChapterRanges([psObject]));

        Assert.Contains("missing Start or End", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeChapterRanges_WhenStartOrEndNotInteger_ThrowsArgumentException()
    {
        var psObject = new PSObject();
        psObject.Properties.Add(new PSNoteProperty("Start", "one"));
        psObject.Properties.Add(new PSNoteProperty("End", 2));

        var exception = Assert.Throws<ArgumentException>(() => ChapterRangeHelper.NormalizeChapterRanges([psObject]));

        Assert.Contains("must be integers", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
