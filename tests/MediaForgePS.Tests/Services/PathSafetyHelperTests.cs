using System;
using System.IO;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class PathSafetyHelperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_PathSafety_" + Guid.NewGuid().ToString("N"));

    public PathSafetyHelperTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void SanitizePathSegment_ReplacesInvalidCharacters()
    {
        var sanitized = PathSafetyHelper.SanitizePathSegment("Show: Title?");

        Assert.DoesNotContain(':', sanitized);
        Assert.DoesNotContain('?', sanitized);
        Assert.Contains('_', sanitized);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public void SanitizePathSegment_RejectsTraversalAndSeparators(string segment)
    {
        Assert.Throws<ArgumentException>(() => PathSafetyHelper.SanitizePathSegment(segment));
    }

    [Theory]
    [InlineData("..\\outside.mkv")]
    [InlineData("../outside.mkv")]
    public void GetContainedFilePath_RejectsParentTraversal(string fileNameSegment)
    {
        Assert.Throws<ArgumentException>(() =>
            PathSafetyHelper.GetContainedFilePath(_tempDir, fileNameSegment));
    }

    [Fact]
    public void GetContainedFilePath_AcceptsSimpleFileName()
    {
        var path = PathSafetyHelper.GetContainedFilePath(_tempDir, "episode.mkv");

        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "episode.mkv")), path);
    }

    [Fact]
    public void GetContainedRelativePath_RejectsEscapeFromRoot()
    {
        Assert.Throws<ArgumentException>(() =>
            PathSafetyHelper.GetContainedRelativePath(_tempDir, Path.Combine("..", "escape.srt")));
    }

    [Fact]
    public void GetContainedRelativePath_AcceptsNestedRelativePath()
    {
        var path = PathSafetyHelper.GetContainedRelativePath(_tempDir, Path.Combine("Season 01", "ep.srt"));

        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "Season 01", "ep.srt")), path);
    }

    [Fact]
    public void EnsurePathUnderRoot_RejectsSiblingEscape()
    {
        var outside = Path.GetFullPath(Path.Combine(_tempDir, "..", "outside.txt"));

        Assert.Throws<ArgumentException>(() =>
            PathSafetyHelper.EnsurePathUnderRoot(_tempDir, outside));
    }
}
