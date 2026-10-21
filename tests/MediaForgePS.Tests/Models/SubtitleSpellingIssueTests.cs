using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Models;

public class SubtitleSpellingIssueTests
{
    [Fact]
    public void HighlightWords_HighlightsAllMisspellingsInLine()
    {
        var highlighted = SubtitleSpellingIssue.HighlightWords(
            "Cartman said hello to Zorpblat.",
            ["Cartman", "Zorpblat"]);

        Assert.Equal(
            "\u001b[91;1mCartman\u001b[0m said hello to \u001b[91;1mZorpblat\u001b[0m.",
            highlighted);
    }

    [Fact]
    public void HighlightWordOccurrence_HighlightsOnlyRequestedInstance()
    {
        var first = SubtitleSpellingIssue.HighlightWordOccurrence("badword and badword", "badword", 0);
        var second = SubtitleSpellingIssue.HighlightWordOccurrence("badword and badword", "badword", 1);

        Assert.Equal("\u001b[91;1mbadword\u001b[0m and badword", first);
        Assert.Equal("badword and \u001b[91;1mbadword\u001b[0m", second);
    }

    [Fact]
    public void HighlightSpan_HighlightsExactRange()
    {
        var highlighted = SubtitleSpellingIssue.HighlightSpan("hello badword world", 6, 7);
        Assert.Equal("hello \u001b[91;1mbadword\u001b[0m world", highlighted);
    }

    [Fact]
    public void HighlightWords_IgnoresLeadingHyphen()
    {
        var highlighted = SubtitleSpellingIssue.HighlightWords("-misspeled word", ["misspeled"]);
        Assert.Equal("-\u001b[91;1mmisspeled\u001b[0m word", highlighted);
    }

    [Fact]
    public void HighlightWords_DoesNotHighlightSubstrings()
    {
        var highlighted = SubtitleSpellingIssue.HighlightWords(
            "catalog cat",
            ["cat"]);

        Assert.Equal("catalog \u001b[91;1mcat\u001b[0m", highlighted);
    }

    [Fact]
    public void Word_JoinsMultipleMisspellings()
    {
        var issue = new SubtitleSpellingIssue(
            "a.srt",
            1,
            "00:00:01,000 --> 00:00:02,000",
            1,
            ["badword", "wrngword"],
            "badword and wrngword",
            SubtitleSpellingIssue.EmptySuggestions);

        Assert.Equal("badword, wrngword", issue.Word);
    }
}
