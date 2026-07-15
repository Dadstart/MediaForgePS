using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitlePathHelperTests
{
    [Theory]
    [InlineData(@"C:\out\movie.eng.sdh.srt", @"C:\out\movie")]
    [InlineData(@"C:\out\movie.3.eng.sdh.sup", @"C:\out\movie")]
    [InlineData(@"C:\out\movie.eng.sdh.sub", @"C:\out\movie")]
    public void GetMediaBaseKeyFromSubtitlePath_ParsesExportNaming(string subtitlePath, string expectedMediaBase)
    {
        var result = SubtitlePathHelper.GetMediaBaseKeyFromSubtitlePath(subtitlePath);

        Assert.Equal(expectedMediaBase, result);
    }

    [Fact]
    public void SelectImagePathsForOcr_Skip_ReturnsEmpty()
    {
        var exported = new[]
        {
            @"C:\out\movie.eng.sdh.sub",
            @"C:\out\movie.eng.sdh.srt",
        };

        var result = SubtitlePathHelper.SelectImagePathsForOcr(exported, SubtitleOcrMode.Skip);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectImagePathsForOcr_Force_IncludesAllSubFiles()
    {
        var exported = new[]
        {
            @"C:\out\movie.eng.sdh.sub",
            @"C:\out\movie.eng.sdh.srt",
            @"C:\out\movie.3.eng.sdh.sup",
        };

        var result = SubtitlePathHelper.SelectImagePathsForOcr(exported, SubtitleOcrMode.Force);

        Assert.Equal(2, result.Count);
        Assert.Contains(@"C:\out\movie.eng.sdh.sub", result);
        Assert.Contains(@"C:\out\movie.3.eng.sdh.sup", result);
    }

    [Fact]
    public void SelectImagePathsForOcr_Auto_IncludesImageSubtitlesOnlyWhenNoSrtExportedForSource()
    {
        var exported = new[]
        {
            @"C:\out\only-sub.eng.sdh.sub",
            @"C:\out\only-sup.eng.sdh.sup",
            @"C:\out\mixed.eng.sdh.sub",
            @"C:\out\mixed.eng.sdh.srt",
            @"C:\out\mixed.3.eng.sdh.sup",
        };

        var result = SubtitlePathHelper.SelectImagePathsForOcr(exported, SubtitleOcrMode.Auto);

        Assert.Equal(2, result.Count);
        Assert.Contains(@"C:\out\only-sub.eng.sdh.sub", result);
        Assert.Contains(@"C:\out\only-sup.eng.sdh.sup", result);
        Assert.DoesNotContain(@"C:\out\mixed.eng.sdh.sub", result);
        Assert.DoesNotContain(@"C:\out\mixed.3.eng.sdh.sup", result);
    }

    [Fact]
    public void SelectImagePathsForOcr_Auto_SkipsWhenMultipleNonSrtFormatsExportedForSource()
    {
        var exported = new[]
        {
            @"C:\out\movie.eng.sdh.sub",
            @"C:\out\movie.3.eng.sdh.sup",
        };

        var result = SubtitlePathHelper.SelectImagePathsForOcr(exported, SubtitleOcrMode.Auto);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(@"C:\out\movie.eng.sdh.sub")]
    [InlineData(@"C:\out\movie.eng.sdh.sup")]
    public void ShouldAutoOcrImageSubtitles_SingleNonSrtFormat_ReturnsTrue(string subtitlePath)
    {
        var result = SubtitlePathHelper.ShouldAutoOcrImageSubtitles([subtitlePath]);

        Assert.True(result);
    }

    [Fact]
    public void ShouldAutoOcrImageSubtitles_SrtOnly_ReturnsFalse()
    {
        var result = SubtitlePathHelper.ShouldAutoOcrImageSubtitles([@"C:\out\movie.eng.sdh.srt"]);

        Assert.False(result);
    }

    [Fact]
    public void ShouldAutoOcrImageSubtitles_MixedSrtAndImageFormats_ReturnsFalse()
    {
        var result = SubtitlePathHelper.ShouldAutoOcrImageSubtitles(
        [
            @"C:\out\mixed.eng.sdh.sub",
            @"C:\out\mixed.eng.sdh.srt",
            @"C:\out\mixed.3.eng.sdh.sup",
        ]);

        Assert.False(result);
    }

    [Fact]
    public void SelectUnusedImageSubtitlePaths_WhenSrtCoexistsWithVobSub_ReturnsImagePaths()
    {
        var exported = new[]
        {
            @"C:\out\movie.eng.sdh.srt",
            @"C:\out\movie.2.eng.sdh.sub",
            @"C:\out\only-sub.eng.sdh.sub",
        };

        var result = SubtitlePathHelper.SelectUnusedImageSubtitlePaths(exported);

        Assert.Equal([@"C:\out\movie.2.eng.sdh.sub"], result);
    }

    [Fact]
    public void SelectUnusedImageSubtitlePaths_WhenOnlyImageSubtitles_ReturnsEmpty()
    {
        var exported = new[]
        {
            @"C:\out\movie.eng.sdh.sub",
            @"C:\out\movie.3.eng.sdh.sup",
        };

        var result = SubtitlePathHelper.SelectUnusedImageSubtitlePaths(exported);

        Assert.Empty(result);
    }
}
