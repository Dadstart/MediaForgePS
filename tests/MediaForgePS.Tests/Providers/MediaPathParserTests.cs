using System;
using System.IO;
using Dadstart.Labs.MediaForge.Providers;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Providers;

public class MediaPathParserTests
{
    [Theory]
    [InlineData("movie.mkv", true)]
    [InlineData("movie.MP4", true)]
    [InlineData("notes.txt", false)]
    [InlineData("archive.zip", false)]
    public void IsMediaFilePath_UsesKnownExtensions(string path, bool expected) =>
        Assert.Equal(expected, MediaPathParser.IsMediaFilePath(path));

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("\\streams\\audio\\0", "streams/audio/0")]
    [InlineData("streams//audio/0/", "streams/audio/0")]
    public void NormalizeProviderPath_NormalizesSeparators(string? path, string expected) =>
        Assert.Equal(expected, MediaPathParser.NormalizeProviderPath(path));

    [Fact]
    public void TryParse_WhenRootIsDirectory_ReturnsDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "mediaforge-provider-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var info = MediaPathParser.TryParse(root, "", File.Exists, Directory.Exists);

            Assert.NotNull(info);
            Assert.Equal(MediaPathKind.FileSystemDirectory, info!.Kind);
            Assert.Equal(Path.GetFullPath(root), info.PhysicalPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryParse_WhenRootIsMediaFile_ReturnsMediaFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "mediaforge-provider-" + Guid.NewGuid().ToString("N") + ".mkv");
        File.WriteAllBytes(root, [0]);
        try
        {
            var info = MediaPathParser.TryParse(root, "", File.Exists, Directory.Exists);

            Assert.NotNull(info);
            Assert.Equal(MediaPathKind.MediaFile, info!.Kind);
            Assert.Equal(Path.GetFullPath(root), info.PhysicalPath);
            Assert.Equal(string.Empty, info.ProviderPath);
        }
        finally
        {
            File.Delete(root);
        }
    }

    [Fact]
    public void TryParse_VirtualNodes_UnderMediaFileRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mediaforge-provider-" + Guid.NewGuid().ToString("N") + ".mkv");
        File.WriteAllBytes(root, [0]);
        try
        {
            Assert.Equal(MediaPathKind.Format, MediaPathParser.TryParse(root, "format", File.Exists, Directory.Exists)!.Kind);
            Assert.Equal(MediaPathKind.Chapters, MediaPathParser.TryParse(root, "chapters", File.Exists, Directory.Exists)!.Kind);
            Assert.Equal(MediaPathKind.Streams, MediaPathParser.TryParse(root, "streams", File.Exists, Directory.Exists)!.Kind);

            var streamType = MediaPathParser.TryParse(root, "streams/audio", File.Exists, Directory.Exists);
            Assert.NotNull(streamType);
            Assert.Equal(MediaPathKind.StreamType, streamType!.Kind);
            Assert.Equal("audio", streamType.StreamType);

            var stream = MediaPathParser.TryParse(root, "streams/audio/0", File.Exists, Directory.Exists);
            Assert.NotNull(stream);
            Assert.Equal(MediaPathKind.Stream, stream!.Kind);
            Assert.Equal("audio", stream.StreamType);
            Assert.Equal(0, stream.Index);

            var allStream = MediaPathParser.TryParse(root, "streams/all/3", File.Exists, Directory.Exists);
            Assert.NotNull(allStream);
            Assert.Equal(MediaPathKind.Stream, allStream!.Kind);
            Assert.Equal("all", allStream.StreamType);
            Assert.Equal(3, allStream.Index);

            var chapter = MediaPathParser.TryParse(root, "chapters/1", File.Exists, Directory.Exists);
            Assert.NotNull(chapter);
            Assert.Equal(MediaPathKind.Chapter, chapter!.Kind);
            Assert.Equal(1, chapter.Index);
        }
        finally
        {
            File.Delete(root);
        }
    }

    [Fact]
    public void TryParse_VirtualNodes_UnderDirectoryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mediaforge-provider-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mediaPath = Path.Combine(root, "clip.mkv");
        File.WriteAllBytes(mediaPath, [0]);
        try
        {
            var media = MediaPathParser.TryParse(root, "clip.mkv", File.Exists, Directory.Exists);
            Assert.NotNull(media);
            Assert.Equal(MediaPathKind.MediaFile, media!.Kind);
            Assert.Equal("clip.mkv", media.ProviderPath);

            var stream = MediaPathParser.TryParse(root, @"clip.mkv\streams\video\0", File.Exists, Directory.Exists);
            Assert.NotNull(stream);
            Assert.Equal(MediaPathKind.Stream, stream!.Kind);
            Assert.Equal("video", stream.StreamType);
            Assert.Equal(0, stream.Index);
            Assert.Equal(Path.GetFullPath(mediaPath), stream.PhysicalPath);
            Assert.Equal("clip.mkv/streams/video/0", stream.ProviderPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryParse_NonMediaFile_IsLeaf()
    {
        var root = Path.Combine(Path.GetTempPath(), "mediaforge-provider-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "readme.txt");
        File.WriteAllText(filePath, "x");
        try
        {
            var info = MediaPathParser.TryParse(root, "readme.txt", File.Exists, Directory.Exists);

            Assert.NotNull(info);
            Assert.Equal(MediaPathKind.FileSystemFile, info!.Kind);
            Assert.Null(MediaPathParser.TryParse(root, "readme.txt/streams", File.Exists, Directory.Exists));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryParse_UnknownVirtualNode_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "mediaforge-provider-" + Guid.NewGuid().ToString("N") + ".mkv");
        File.WriteAllBytes(root, [0]);
        try
        {
            Assert.Null(MediaPathParser.TryParse(root, "tracks", File.Exists, Directory.Exists));
            Assert.Null(MediaPathParser.TryParse(root, "streams/foo/0", File.Exists, Directory.Exists));
            Assert.Null(MediaPathParser.TryParse(root, "chapters/x", File.Exists, Directory.Exists));
        }
        finally
        {
            File.Delete(root);
        }
    }

    [Fact]
    public void TryParse_RejectsPathsOutsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mediaforge-provider-root-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "mediaforge-provider-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "secret.txt");
        File.WriteAllText(outsideFile, "x");
        try
        {
            var relativeEscape = Path.Combine("..", Path.GetFileName(outside), "secret.txt");
            Assert.Null(MediaPathParser.TryParse(root, relativeEscape, File.Exists, Directory.Exists));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }
}
