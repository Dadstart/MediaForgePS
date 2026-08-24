using System;
using System.IO;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class PathHelperTests
{
    [Theory]
    [InlineData(@"C:\media\out.mp4", "out.mp4")]
    [InlineData(@"C:\out.mp4", "out.mp4")]
    [InlineData("/home/user/out.mp4", "out.mp4")]
    [InlineData("out.mp4", "out.mp4")]
    [InlineData(@"mixed/path\file.mkv", "file.mkv")]
    [InlineData("", "")]
    public void GetFileName_TreatsBothSeparatorsAsDirectoryBoundaries(string path, string expected)
    {
        Assert.Equal(expected, PathHelper.GetFileName(path));
    }

    [Fact]
    public void ResolveAbsolutePath_WithRootedPath_ReturnsFullPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "sub", "file.mkv");
        var currentLocation = Path.Combine(Path.GetTempPath(), "other");

        var result = PathHelper.ResolveAbsolutePath(path, currentLocation);

        Assert.Equal(Path.GetFullPath(path), result);
    }

    [Fact]
    public void ResolveAbsolutePath_WithRelativePath_CombinesWithCurrentLocation()
    {
        var path = "relative" + Path.DirectorySeparatorChar + "file.mkv";
        var currentLocation = Path.GetTempPath();
        var expected = Path.GetFullPath(Path.Combine(currentLocation, path));

        var result = PathHelper.ResolveAbsolutePath(path, currentLocation);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveAbsolutePath_WithRelativePathNoSeparator_CombinesWithCurrentLocation()
    {
        var path = "output";
        var currentLocation = Path.GetTempPath();
        var expected = Path.GetFullPath(Path.Combine(currentLocation, path));

        var result = PathHelper.ResolveAbsolutePath(path, currentLocation);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveOutputDirectory_WithNullOutputPath_ReturnsInputFileDirectory()
    {
        var resolvedInputPath = Path.Combine(Path.GetTempPath(), "input", "video.mkv");

        var result = PathHelper.ResolveOutputDirectory(
            null,
            resolvedInputPath,
            Path.GetTempPath(),
            _ => (false, null));

        Assert.Equal(Path.GetDirectoryName(resolvedInputPath), result);
    }

    [Fact]
    public void ResolveOutputDirectory_WithWhitespaceOutputPath_ReturnsInputFileDirectory()
    {
        var resolvedInputPath = Path.Combine(Path.GetTempPath(), "input", "video.mkv");

        var result = PathHelper.ResolveOutputDirectory(
            "   ",
            resolvedInputPath,
            Path.GetTempPath(),
            _ => (false, null));

        Assert.Equal(Path.GetDirectoryName(resolvedInputPath), result);
    }

    [Fact]
    public void ResolveOutputDirectory_WithOutputPathAndResolverSuccess_ReturnsResolvedDirectory()
    {
        var resolvedInputPath = Path.Combine(Path.GetTempPath(), "input", "video.mkv");
        var outputDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_PathHelper_" + Guid.NewGuid().ToString("N"));
        var resolvedFile = Path.Combine(outputDir, "dummy.mkv");

        try
        {
            var result = PathHelper.ResolveOutputDirectory(
                outputDir,
                resolvedInputPath,
                Path.GetTempPath(),
                path =>
                {
                    if (path.EndsWith("dummy.mkv", StringComparison.OrdinalIgnoreCase))
                        return (true, resolvedFile);
                    return (false, null);
                });

            Assert.Equal(outputDir, result);
            Assert.True(Directory.Exists(outputDir));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir);
        }
    }

    [Fact]
    public void ResolveOutputDirectory_WithOutputPathAndResolverFailure_CreatesDirectoryUnderCurrentLocation()
    {
        var resolvedInputPath = Path.Combine(Path.GetTempPath(), "input", "video.mkv");
        var currentLocation = Path.GetTempPath();
        var outputPath = "PathHelper_Out_" + Guid.NewGuid().ToString("N");
        var expectedDir = Path.GetFullPath(Path.Combine(currentLocation, outputPath));

        try
        {
            var result = PathHelper.ResolveOutputDirectory(
                outputPath,
                resolvedInputPath,
                currentLocation,
                _ => (false, null));

            Assert.Equal(expectedDir, result);
            Assert.True(Directory.Exists(expectedDir));
        }
        finally
        {
            if (Directory.Exists(expectedDir))
                Directory.Delete(expectedDir);
        }
    }

    [Fact]
    public void ResolveOutputDirectory_WithOutputPathTrimsWhitespace()
    {
        var resolvedInputPath = Path.Combine(Path.GetTempPath(), "input", "video.mkv");
        var currentLocation = Path.GetTempPath();
        var outputPath = "  PathHelper_Trim_" + Guid.NewGuid().ToString("N") + "  ";
        var expectedDir = Path.GetFullPath(Path.Combine(currentLocation, outputPath.Trim()));

        try
        {
            var result = PathHelper.ResolveOutputDirectory(
                outputPath,
                resolvedInputPath,
                currentLocation,
                _ => (false, null));

            Assert.Equal(expectedDir, result);
        }
        finally
        {
            if (Directory.Exists(expectedDir))
                Directory.Delete(expectedDir);
        }
    }

    [Fact]
    public void IsSameVolume_SameRoot_ReturnsTrue()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var path1 = Path.Combine(root, "media", "a.mp4");
        var path2 = Path.Combine(root, "plex", "nested", "b.mp4");

        Assert.True(PathHelper.IsSameVolume(path1, path2));
    }

    [Fact]
    public void IsSameVolume_DifferentRoots_ReturnsFalse()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.False(PathHelper.IsSameVolume(@"C:\media\a.mp4", @"D:\media\b.mp4"));
    }

    [Fact]
    public void MoveFile_SameVolume_MovesSourceToDestination()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_PathHelper_Move_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "source.txt");
        var destinationPath = Path.Combine(tempDir, "nested", "destination.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.WriteAllText(sourcePath, "payload");

        try
        {
            var result = PathHelper.MoveFile(sourcePath, destinationPath);

            Assert.True(result.SourceRemoved);
            Assert.Null(result.SourceDeleteError);
            Assert.False(File.Exists(sourcePath));
            Assert.Equal("payload", File.ReadAllText(destinationPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DeleteSourceAfterCopy_WhenSourceIsReadOnly_ReturnsDeleteErrorWithoutThrowing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_PathHelper_Delete_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "source.txt");
        File.WriteAllText(sourcePath, "payload");
        MakeSourceDeleteBlocked(sourcePath, tempDir);

        try
        {
            var result = PathHelper.DeleteSourceAfterCopy(sourcePath);

            Assert.False(result.SourceRemoved);
            Assert.NotNull(result.SourceDeleteError);
            Assert.True(File.Exists(sourcePath));
        }
        finally
        {
            RestoreSourceDeletePermissions(sourcePath, tempDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void MakeSourceDeleteBlocked(string sourcePath, string containingDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(sourcePath, FileAttributes.ReadOnly);
            return;
        }

        // On Unix, delete requires write permission on the containing directory, not the file mode.
        File.SetUnixFileMode(
            containingDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private static void RestoreSourceDeletePermissions(string sourcePath, string containingDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            if (File.Exists(sourcePath))
                File.SetAttributes(sourcePath, FileAttributes.Normal);

            return;
        }

        if (!Directory.Exists(containingDirectory))
            return;

        File.SetUnixFileMode(
            containingDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
    }
}
