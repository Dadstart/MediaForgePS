using System.Linq;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfmpegArgumentBuilderTests
{
    [Fact]
    public void AddTitleMetadata_DoesNotPreQuoteTitleValue()
    {
        var args = new FfmpegArgumentBuilder()
            .AddTitleMetadata('a', 0, "English \"5.1\"")
            .ToArguments()
            .ToArray();

        Assert.Equal(["-metadata:s:a:0", "title=English \"5.1\""], args);
    }

    [Fact]
    public void AddTitleMetadata_EscapesEqualsSemicolonHashBackslashAndNewline()
    {
        var args = new FfmpegArgumentBuilder()
            .AddTitleMetadata('a', 0, "a=b;c#d\\e\nf")
            .ToArguments()
            .ToArray();

        Assert.Equal(["-metadata:s:a:0", "title=a\\=b\\;c\\#d\\\\e\\\nf"], args);
    }

    [Theory]
    [InlineData("plain title", "plain title")]
    [InlineData("a=b", "a\\=b")]
    [InlineData("a;b", "a\\;b")]
    [InlineData("a#b", "a\\#b")]
    [InlineData("a\\b", "a\\\\b")]
    [InlineData("line1\nline2", "line1\\\nline2")]
    public void EscapeMetadataValue_EscapesFfmpegSpecialCharacters(string input, string expected)
    {
        var result = FfmpegArgumentBuilder.EscapeMetadataValue(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void EscapeMetadataValue_EscapesAllSpecialCharactersTogether()
    {
        var result = FfmpegArgumentBuilder.EscapeMetadataValue("=;#\\\n");

        Assert.Equal(@"\=\;\#\\" + "\\\n", result);
    }

    [Fact]
    public void AddTitleMetadata_WithNullOrWhitespace_OmitsArgument()
    {
        var args = new FfmpegArgumentBuilder()
            .AddTitleMetadata('a', 0, null)
            .AddTitleMetadata('a', 1, "   ")
            .ToArguments()
            .ToArray();

        Assert.Empty(args);
    }
}
