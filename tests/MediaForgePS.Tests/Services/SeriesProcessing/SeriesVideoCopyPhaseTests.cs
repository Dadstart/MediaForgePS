using System;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Models;
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
    public void CopyVideoFilesWithMetadata_CopiesFilesUsingEpisodeMetadata()
    {
        var sourceDir = Path.Combine(_root, "source");
        var destinationDir = Path.Combine(_root, "dest");
        Directory.CreateDirectory(sourceDir);
        CreateFile(sourceDir, "episode.mkv", "large-enough-content");
        var io = CreatePathContext(sourceDir);
        var episodes = new[] { new TvDbEpisodeInfo("42", 1, "Pilot", 1) };
        var request = new VideoCopyRequest([sourceDir], destinationDir, "Show", 1, episodes, ["*.mkv"], 1, 0, false);

        var copied = _phase.CopyVideoFilesWithMetadata(io, request, SeriesProcessingService.BuildEpisodeFileName);

        var copiedPath = Assert.Single(copied);
        Assert.Equal(Path.Combine(destinationDir, "Show {tvdb 42} - s01e01.mkv"), copiedPath);
        Assert.True(File.Exists(copiedPath));
        Assert.Contains(io.ProgressRecords, record => record.Activity.Contains("Video copy", StringComparison.Ordinal));
    }

    [Fact]
    public void CopyVideoFilesWithMetadata_WhenDestinationExistsAndForceFalse_SkipsCopyAndAddsWarning()
    {
        var sourceDir = Path.Combine(_root, "source-skip");
        var destinationDir = Path.Combine(_root, "dest-skip");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);
        CreateFile(sourceDir, "episode.mkv", "large-enough-content");
        var destinationPath = Path.Combine(destinationDir, "Show {tvdb 1} - s01e01.mkv");
        File.WriteAllText(destinationPath, "existing");
        var io = CreatePathContext(sourceDir);
        var request = new VideoCopyRequest([sourceDir], destinationDir, "Show", 1, CreateEpisodes(), ["*.mkv"], 1, 0, false);

        var copied = _phase.CopyVideoFilesWithMetadata(io, request, SeriesProcessingService.BuildEpisodeFileName);

        Assert.Equal(destinationPath, Assert.Single(copied));
        Assert.Equal("existing", File.ReadAllText(destinationPath));
        Assert.Contains(io.Warnings, warning => warning.Contains("skipping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CopyVideoFilesWithMetadata_WhenDestinationExistsAndForceTrue_OverwritesFile()
    {
        var sourceDir = Path.Combine(_root, "source-force");
        var destinationDir = Path.Combine(_root, "dest-force");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);
        CreateFile(sourceDir, "episode.mkv", "new-content");
        var destinationPath = Path.Combine(destinationDir, "Show {tvdb 1} - s01e01.mkv");
        File.WriteAllText(destinationPath, "existing");
        var io = CreatePathContext(sourceDir);
        var request = new VideoCopyRequest([sourceDir], destinationDir, "Show", 1, CreateEpisodes(), ["*.mkv"], 1, 0, true);

        var copied = _phase.CopyVideoFilesWithMetadata(io, request, SeriesProcessingService.BuildEpisodeFileName);

        Assert.Equal(destinationPath, Assert.Single(copied));
        Assert.Equal("new-content", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void CopyVideoFilesWithMetadata_WhenEpisodeMetadataRunsOut_WritesWarningAndStops()
    {
        var sourceDir = Path.Combine(_root, "source-overflow");
        var destinationDir = Path.Combine(_root, "dest-overflow");
        Directory.CreateDirectory(sourceDir);
        CreateFile(sourceDir, "episode-1.mkv", "large-enough-content");
        CreateFile(sourceDir, "episode-2.mkv", "large-enough-content");
        var io = CreatePathContext(sourceDir);
        var request = new VideoCopyRequest([sourceDir], destinationDir, "Show", 1, CreateEpisodes(), ["*.mkv"], 1, 0, false);

        var copied = _phase.CopyVideoFilesWithMetadata(io, request, SeriesProcessingService.BuildEpisodeFileName);

        Assert.Single(copied);
        Assert.Contains(io.Warnings, warning => warning.Contains("No TVDb episode metadata", StringComparison.Ordinal));
    }

    [Fact]
    public void CopyVideoFilesWithMetadata_WhenEpisodeStartIsTwo_MapsFirstFileToEpisodeTwo()
    {
        var sourceDir = Path.Combine(_root, "source-offset");
        var destinationDir = Path.Combine(_root, "dest-offset");
        Directory.CreateDirectory(sourceDir);
        CreateFile(sourceDir, "episode.mkv", "large-enough-content");
        var io = CreatePathContext(sourceDir);
        var episodes = new[]
        {
            new TvDbEpisodeInfo("1", 1, "Pilot", 1),
            new TvDbEpisodeInfo("2", 1, "Second", 2)
        };
        var request = new VideoCopyRequest([sourceDir], destinationDir, "Show", 1, episodes, ["*.mkv"], 2, 0, false);

        var copied = _phase.CopyVideoFilesWithMetadata(io, request, SeriesProcessingService.BuildEpisodeFileName);

        Assert.Equal(Path.Combine(destinationDir, "Show {tvdb 2} - s01e02.mkv"), Assert.Single(copied));
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

    private static TvDbEpisodeInfo[] CreateEpisodes() =>
        [new TvDbEpisodeInfo("1", 1, "Pilot", 1)];

    private static FakeCmdletIO CreatePathContext(string sourceDir)
    {
        var io = new FakeCmdletIO();
        io.Paths.ResolveProviderPaths = _ => [sourceDir];
        return io;
    }
}
