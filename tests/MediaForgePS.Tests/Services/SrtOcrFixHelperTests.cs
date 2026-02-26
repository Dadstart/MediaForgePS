using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SrtOcrFixHelperTests
{
    [Fact]
    public void FixMusicNoteOcrErrors_PreservesStructure_WhenNoFixesNeeded()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nHello world.\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(srt);
        Assert.Contains("1", result);
        Assert.Contains("00:00:01,000 --> 00:00:02,000", result);
        Assert.Contains("Hello world.", result);
    }

    [Fact]
    public void FixMusicNoteOcrErrors_ReplacesStandaloneJ_WithMusicNote()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nSong J plays.\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(srt);
        Assert.Contains("Song ♪ plays.", result);
    }

    [Fact]
    public void FixMusicNoteOcrErrors_ReplacesStandalonePipe_WithI()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nI think | am right.\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(srt);
        Assert.Contains("I think I am right.", result);
    }

    [Fact]
    public void FixMusicNoteOcrErrors_ReplacesTrailingI_WithMusicNote()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nLyric line I\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(srt);
        Assert.Contains("Lyric line ♪", result);
    }

    [Fact]
    public void FixMusicNoteOcrErrors_ReplacesDown10South_WithDownToSouth()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nI'm goin' down 10 South Park,\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(srt);
        Assert.Contains("down to South Park", result);
    }

    [Fact]
    public void FixMusicNoteOcrErrors_ReplacesUnmatchedBracket_WithI()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nHello ] world\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(srt);
        Assert.Contains("Hello I world", result);
    }

    [Fact]
    public void FixMusicNoteOcrErrors_LeavesMatchedBrackets()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nHello [world] here\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(srt);
        Assert.Contains("Hello [world] here", result);
    }

    [Fact]
    public void FixMusicNoteOcrErrors_PreservesMultipleBlocks()
    {
        var content = "1\n00:00:01,000 --> 00:00:02,000\nLine one.\n\n2\n00:00:02,000 --> 00:00:03,000\nLine two.\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(content);
        Assert.Contains("1", result);
        Assert.Contains("Line one.", result);
        Assert.Contains("2", result);
        Assert.Contains("Line two.", result);
    }

    [Fact]
    public void FixMusicNoteOcrErrors_HandlesItalicTags()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\n</i>J <i> plays.\n\n";
        var result = SrtOcrFixHelper.FixMusicNoteOcrErrors(srt);
        Assert.Contains("</i>♪ <i> plays.", result);
    }
}
