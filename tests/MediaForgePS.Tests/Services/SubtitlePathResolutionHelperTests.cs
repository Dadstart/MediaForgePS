using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitlePathResolutionHelperTests
{
    [Fact]
    public void CollectInputPaths_WhenNull_DoesNothing()
    {
        var destination = new List<string>();

        SubtitlePathResolutionHelper.CollectInputPaths(null, destination);

        Assert.Empty(destination);
    }

    [Fact]
    public void CollectInputPaths_TrimsAndSkipsWhitespace()
    {
        var destination = new List<string>();

        SubtitlePathResolutionHelper.CollectInputPaths(["  one.srt  ", "", "   ", "two.srt"], destination);

        Assert.Equal(["one.srt", "two.srt"], destination);
    }

    [Fact]
    public void EnumerateMatchingPaths_WhenSingleFile_IncludesMatchingFile()
    {
        using var temp = new TempDirectory();
        var srtPath = temp.CreateFile("clip.srt", "content");

        var result = SubtitlePathResolutionHelper.EnumerateMatchingPaths(
            [(srtPath, false)],
            SearchOption.TopDirectoryOnly,
            "*.srt",
            SubtitlePathHelper.IsSrtPath);

        Assert.Equal([srtPath], result);
    }

    [Fact]
    public void EnumerateMatchingPaths_WhenDirectory_RecursivelyFindsMatches()
    {
        using var temp = new TempDirectory();
        var nestedDir = temp.CreateDirectory("nested");
        var nestedSrt = Path.Combine(nestedDir, "nested.srt");
        File.WriteAllText(nestedSrt, "content");
        temp.CreateFile("root.srt", "content");
        temp.CreateFile("ignore.txt", "content");

        var result = SubtitlePathResolutionHelper.EnumerateMatchingPaths(
            [(temp.Path, true)],
            SearchOption.AllDirectories,
            "*.srt",
            SubtitlePathHelper.IsSrtPath);

        Assert.Equal(2, result.Count);
        Assert.Contains(Path.Combine(temp.Path, "root.srt"), result);
        Assert.Contains(nestedSrt, result);
    }

    [Fact]
    public void EnumerateMatchingPaths_DeduplicatesPathsCaseInsensitively()
    {
        using var temp = new TempDirectory();
        var srtPath = temp.CreateFile("clip.srt", "content");
        var duplicatePath = srtPath.ToUpperInvariant();

        var result = SubtitlePathResolutionHelper.EnumerateMatchingPaths(
            [(srtPath, false), (duplicatePath, false)],
            SearchOption.TopDirectoryOnly,
            "*.srt",
            SubtitlePathHelper.IsSrtPath);

        Assert.Single(result);
    }

    [Fact]
    public void EnumerateMatchingPaths_WhenIncludePathRejects_SkipsPath()
    {
        using var temp = new TempDirectory();
        var srtPath = temp.CreateFile("clip.srt", "content");
        var txtPath = temp.CreateFile("clip.txt", "content");

        var result = SubtitlePathResolutionHelper.EnumerateMatchingPaths(
            [(srtPath, false), (txtPath, false)],
            SearchOption.TopDirectoryOnly,
            "*.*",
            SubtitlePathHelper.IsSrtPath);

        Assert.Equal([srtPath], result);
    }

    [Fact]
    public void ResolveFileOrDirectoryPaths_WhenFileExists_ReturnsFilePair()
    {
        using var temp = new TempDirectory();
        var srtPath = temp.CreateFile("clip.srt", "content");
        var io = new FakeCmdletIO();
        var errors = new List<ErrorRecord>();

        var result = SubtitlePathResolutionHelper.ResolveFileOrDirectoryPaths(
            io.Paths,
            [srtPath],
            NullLogger.Instance,
            errors.Add);

        var pair = Assert.Single(result);
        Assert.Equal(srtPath, pair.ResolvedPath);
        Assert.False(pair.IsDirectory);
        Assert.Empty(errors);
    }

    [Fact]
    public void ResolveFileOrDirectoryPaths_WhenDirectoryExists_ReturnsDirectoryPair()
    {
        using var temp = new TempDirectory();
        var io = new FakeCmdletIO();
        var errors = new List<ErrorRecord>();

        var result = SubtitlePathResolutionHelper.ResolveFileOrDirectoryPaths(
            io.Paths,
            [temp.Path],
            NullLogger.Instance,
            errors.Add);

        var pair = Assert.Single(result);
        Assert.Equal(temp.Path, pair.ResolvedPath);
        Assert.True(pair.IsDirectory);
        Assert.Empty(errors);
    }

    [Fact]
    public void ResolveFileOrDirectoryPaths_WhenPathMissing_WritesError()
    {
        using var temp = new TempDirectory();
        var missingPath = Path.Combine(temp.Path, "missing.srt");
        var io = new FakeCmdletIO
        {
            Paths =
            {
                ResolveProviderPaths = _ => [],
                ResolveUnresolvedProviderPath = _ => missingPath
            }
        };
        var errors = new List<ErrorRecord>();

        var result = SubtitlePathResolutionHelper.ResolveFileOrDirectoryPaths(
            io.Paths,
            [missingPath],
            NullLogger.Instance,
            errors.Add);

        Assert.Empty(result);
        var error = Assert.Single(errors);
        Assert.Equal("PathNotFound", error.FullyQualifiedErrorId);
        Assert.Equal(ErrorCategory.ObjectNotFound, error.CategoryInfo.Category);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "MediaForgePS_SubtitlePathResolution_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateFile(string fileName, string content)
        {
            var filePath = global::System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        public string CreateDirectory(string directoryName)
        {
            var directoryPath = global::System.IO.Path.Combine(Path, directoryName);
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
