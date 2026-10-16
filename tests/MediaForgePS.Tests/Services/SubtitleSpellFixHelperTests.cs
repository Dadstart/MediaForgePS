using System;
using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitleSpellFixHelperTests
{
    [Fact]
    public void TryFindNextMisspelling_FindsFirstUnknownWord()
    {
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = SubtitleSpellFixHelper.TryFindNextMisspelling(
            "hello misspeled world",
            searchFrom: 0,
            ignore,
            word => word is "hello" or "world",
            out var word,
            out var start,
            out var length);

        Assert.True(found);
        Assert.Equal("misspeled", word);
        Assert.Equal(6, start);
        Assert.Equal(9, length);
    }

    [Fact]
    public void TryFindNextMisspelling_RespectsSearchFromAndIgnore()
    {
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cartman" };
        var found = SubtitleSpellFixHelper.TryFindNextMisspelling(
            "Cartman said Zorpblat",
            searchFrom: 0,
            ignore,
            _ => false,
            out var word,
            out _,
            out _);

        Assert.True(found);
        Assert.Equal("said", word);

        ignore.Add("said");
        found = SubtitleSpellFixHelper.TryFindNextMisspelling(
            "Cartman said Zorpblat",
            searchFrom: 0,
            ignore,
            _ => false,
            out word,
            out _,
            out _);

        Assert.True(found);
        Assert.Equal("Zorpblat", word);
    }

    [Fact]
    public void ReplaceAt_PreservesTitleCase()
    {
        var result = SubtitleSpellFixHelper.ReplaceAt("Misspeled word", 0, 9, "Misspeled", "misspelled");
        Assert.Equal("Misspelled word", result);
    }

    [Fact]
    public void MatchCasing_AllCaps()
    {
        Assert.Equal("OKAY", SubtitleSpellFixHelper.MatchCasing("OK", "okay"));
    }

    [Fact]
    public void FormatSrt_RebuildsBlocks()
    {
        var blocks = SrtDocumentHelper.ParseSrtBlocks(
            "1\n00:00:01,000 --> 00:00:02,000\nHello world.\n\n");
        blocks[0].TextLines[0] = "Hello there.";

        var formatted = SubtitleSpellFixHelper.FormatSrt(blocks);
        Assert.Contains("Hello there.", formatted);
        Assert.Contains("1", formatted);
        Assert.Contains("00:00:01,000 --> 00:00:02,000", formatted);
    }

    [Fact]
    public void GetSurroundingLines_ReturnsNeighborsAcrossCues()
    {
        var blocks = SrtDocumentHelper.ParseSrtBlocks(
            """
            1
            00:00:01,000 --> 00:00:02,000
            First cue.

            2
            00:00:02,000 --> 00:00:03,000
            Second cue.
            Extra line.

            3
            00:00:03,000 --> 00:00:04,000
            Third cue.
            """);

        var (before, after) = SubtitleSpellFixHelper.GetSurroundingLines(blocks, blockIndex: 1, lineIndex: 0, radius: 2);

        Assert.Equal(["[cue 1] First cue."], before);
        Assert.Equal(["[cue 2] Extra line.", "[cue 3] Third cue."], after);
    }
}
