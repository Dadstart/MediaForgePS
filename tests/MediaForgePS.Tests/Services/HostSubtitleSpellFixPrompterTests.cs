using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class HostSubtitleSpellFixPrompterTests
{
    [Fact]
    public void BuildChoiceMessage_ShowsFullLineWithMisspellingMarked()
    {
        var prompt = CreatePrompt();

        var message = HostSubtitleSpellFixPrompter.BuildChoiceMessage(prompt);

        Assert.Equal("Line: This line has a >>>misspeled<<< word.", message);
    }

    [Theory]
    [InlineData("1", 3, 0)]
    [InlineData("[2]", 3, 1)]
    [InlineData("3", 3, 2)]
    public void TryParseSuggestionChoice_ParsesValidIndexes(string input, int count, int expectedIndex)
    {
        Assert.True(HostSubtitleSpellFixPrompter.TryParseSuggestionChoice(input, count, out var index));
        Assert.Equal(expectedIndex, index);
    }

    [Theory]
    [InlineData("0", 3)]
    [InlineData("4", 3)]
    [InlineData("c", 3)]
    [InlineData("", 3)]
    public void TryParseSuggestionChoice_RejectsInvalidInput(string input, int count)
    {
        Assert.False(HostSubtitleSpellFixPrompter.TryParseSuggestionChoice(input, count, out _));
    }

    [Fact]
    public void EffectiveSurroundingLines_DefaultToEmpty()
    {
        var prompt = CreatePrompt();
        Assert.Empty(prompt.EffectiveLinesBefore);
        Assert.Empty(prompt.EffectiveLinesAfter);
    }

    [Fact]
    public void EffectiveSurroundingLines_ExposeProvidedContext()
    {
        var prompt = CreatePrompt(
            linesBefore: ["[cue 1] Before."],
            linesAfter: ["[cue 2] After."]);

        Assert.Equal(["[cue 1] Before."], prompt.EffectiveLinesBefore);
        Assert.Equal(["[cue 2] After."], prompt.EffectiveLinesAfter);
    }

    private static SubtitleSpellFixPrompt CreatePrompt(
        IReadOnlyList<string>? linesBefore = null,
        IReadOnlyList<string>? linesAfter = null) =>
        new(
            Path: "a.srt",
            CueNumber: 1,
            Timestamp: "00:00:01,000 --> 00:00:02,000",
            LineNumber: 1,
            Word: "misspeled",
            LineText: "This line has a misspeled word.",
            HighlightStart: 16,
            HighlightLength: 9,
            HighlightedLine: "This line has a misspeled word.",
            Suggestions: [],
            LinesBefore: linesBefore,
            LinesAfter: linesAfter);
}
