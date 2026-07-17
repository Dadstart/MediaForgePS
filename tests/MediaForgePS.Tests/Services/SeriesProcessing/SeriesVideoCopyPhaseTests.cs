using System;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.SeriesProcessing;

public class SeriesVideoCopyPhaseTests : IDisposable
{
    private readonly string _root;
    private readonly SeriesVideoCopyPhase _phase = new();

    public SeriesVideoCopyPhaseTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "MediaForgePS-VideoCopy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void GetFilteredVideoFiles_ReturnsFilesInNaturalSortOrder()
    {
        CreateFile(_root, "Episode 10.mkv");
        CreateFile(_root, "Episode 2.mkv");
        CreateFile(_root, "episode 1.mkv");

        var result = _phase.GetFilteredVideoFiles(new FakeCmdletIO(), [_root], ["*.mkv"], minimumFileSizeBytes: 0);

        Assert.Equal(
            ["episode 1.mkv", "Episode 2.mkv", "Episode 10.mkv"],
            result.Select(Path.GetFileName));
    }

    [Fact]
    public void GetFilteredVideoFiles_SortsAcrossMultipleDirectories()
    {
        var discTwo = Path.Combine(_root, "Disc 2");
        var discTen = Path.Combine(_root, "Disc 10");
        Directory.CreateDirectory(discTwo);
        Directory.CreateDirectory(discTen);
        CreateFile(discTen, "title_01.mkv");
        CreateFile(discTwo, "title_01.mkv");

        var result = _phase.GetFilteredVideoFiles(new FakeCmdletIO(), [discTen, discTwo], ["*.mkv"], minimumFileSizeBytes: 0);

        Assert.Equal(
            [Path.Combine(discTwo, "title_01.mkv"), Path.Combine(discTen, "title_01.mkv")],
            result);
    }

    [Fact]
    public void GetFilteredVideoFiles_ExcludesFilesAtOrBelowMinimumSize()
    {
        CreateFile(_root, "small.mkv", "x");
        CreateFile(_root, "large.mkv", "large enough content");

        var result = _phase.GetFilteredVideoFiles(new FakeCmdletIO(), [_root], ["*.mkv"], minimumFileSizeBytes: 5);

        var file = Assert.Single(result);
        Assert.Equal("large.mkv", Path.GetFileName(file));
    }

    private static void CreateFile(string directory, string name, string content = "video")
        => File.WriteAllText(Path.Combine(directory, name), content);
}
