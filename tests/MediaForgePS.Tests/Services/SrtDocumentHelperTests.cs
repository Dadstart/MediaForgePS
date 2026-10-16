using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SrtDocumentHelperTests
{
    [Fact]
    public void ParseSrtBlocks_ParsesCueFields()
    {
        var content = "1\n00:00:01,000 --> 00:00:02,000\nHello <i>world</i>.\nSecond line.\n\n";
        var blocks = SrtDocumentHelper.ParseSrtBlocks(content);

        Assert.Single(blocks);
        Assert.Equal("1", blocks[0].SequenceLine);
        Assert.Equal("00:00:01,000 --> 00:00:02,000", blocks[0].TimestampLine);
        Assert.Equal(2, blocks[0].TextLines.Count);
        Assert.Equal("Hello <i>world</i>.", blocks[0].TextLines[0]);
        Assert.Equal("Second line.", blocks[0].TextLines[1]);
    }

    [Fact]
    public void ParseSrtBlocks_NormalizesCrlfAndParsesMultipleCues()
    {
        var content = "1\r\n00:00:01,000 --> 00:00:02,000\r\nOne.\r\n\r\n2\r\n00:00:03,000 --> 00:00:04,000\r\nTwo.\r\n";
        var blocks = SrtDocumentHelper.ParseSrtBlocks(content);

        Assert.Equal(2, blocks.Count);
        Assert.Equal("One.", blocks[0].TextLines[0]);
        Assert.Equal("Two.", blocks[1].TextLines[0]);
    }

    [Fact]
    public void ParseSrtBlocks_SkipsMalformedShortParts()
    {
        var content = "1\n00:00:01,000 --> 00:00:02,000\n\n2\n00:00:03,000 --> 00:00:04,000\nValid.\n\n";
        var blocks = SrtDocumentHelper.ParseSrtBlocks(content);

        Assert.Single(blocks);
        Assert.Equal("2", blocks[0].SequenceLine);
        Assert.Equal("Valid.", blocks[0].TextLines[0]);
    }
}
