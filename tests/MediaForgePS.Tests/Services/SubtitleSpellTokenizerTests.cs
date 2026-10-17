using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitleSpellTokenizerTests
{
    [Fact]
    public void StripFormatting_RemovesHtmlAndAssOverrides()
    {
        var result = SubtitleSpellTokenizer.StripFormatting("<i>Hello</i> {\\an8}world");
        Assert.Equal("Hello world", result);
    }

    [Fact]
    public void Tokenize_KeepsApostrophesInContractions()
    {
        var tokens = SubtitleSpellTokenizer.Tokenize("They don't know.");
        Assert.Contains("They", tokens);
        Assert.Contains("don't", tokens);
        Assert.Contains("know", tokens);
    }

    [Fact]
    public void Tokenize_SkipsNumbersMusicNotesAndShortAcronyms()
    {
        var tokens = SubtitleSpellTokenizer.Tokenize("♪ 123 FBI hello");
        Assert.DoesNotContain("♪", tokens);
        Assert.DoesNotContain("123", tokens);
        Assert.DoesNotContain("FBI", tokens);
        Assert.Contains("hello", tokens);
    }

    [Fact]
    public void Tokenize_StripsTagsBeforeSplitting()
    {
        var tokens = SubtitleSpellTokenizer.Tokenize("<i>misspeled</i> word");
        Assert.Contains("misspeled", tokens);
        Assert.Contains("word", tokens);
        Assert.DoesNotContain("<i>", tokens);
    }

    [Fact]
    public void ShouldSkipToken_ReturnsTrueForEmptyAndSymbols()
    {
        Assert.True(SubtitleSpellTokenizer.ShouldSkipToken(string.Empty));
        Assert.True(SubtitleSpellTokenizer.ShouldSkipToken("♪"));
        Assert.True(SubtitleSpellTokenizer.ShouldSkipToken("42"));
        Assert.True(SubtitleSpellTokenizer.ShouldSkipToken("OK"));
        Assert.False(SubtitleSpellTokenizer.ShouldSkipToken("hello"));
    }
}
