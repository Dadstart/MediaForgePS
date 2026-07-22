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
}
