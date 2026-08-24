using System;
using System.Collections.Generic;
using System.IO;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.BonusProcessing;

public class BonusProcessingServiceTests
{
    [Fact]
    public void PlexLayout_DefinesExpectedBonusFoldersAndSuffixes()
    {
        var layout = new BonusProcessingService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BonusProcessingService>.Instance,
            null!,
            null!,
            null!,
            null!).PlexLayout;

        Assert.Equal(8, layout.Count);

        Assert.Contains(layout, p => p.FolderName == "Behind The Scenes" && p.Suffix == "behindthescenes");
        Assert.Contains(layout, p => p.FolderName == "Deleted Scenes" && p.Suffix == "deleted");
        Assert.Contains(layout, p => p.FolderName == "Featurettes" && p.Suffix == "featurette");
        Assert.Contains(layout, p => p.FolderName == "Interviews" && p.Suffix == "interview");
        Assert.Contains(layout, p => p.FolderName == "Scenes" && p.Suffix == "scene");
        Assert.Contains(layout, p => p.FolderName == "Shorts" && p.Suffix == "short");
        Assert.Contains(layout, p => p.FolderName == "Trailers" && p.Suffix == "trailer");
        Assert.Contains(layout, p => p.FolderName == "Other" && p.Suffix == "other");
    }

    [Fact]
    public void GetBonusMkvPaths_ReturnsOnlyTopLevelBonusSuffixFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS-BonusDiscovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);

        try
        {
            File.WriteAllText(Path.Combine(root, "movie-trailer.mkv"), "a");
            File.WriteAllText(Path.Combine(root, "movie-feature.mkv"), "b");
            File.WriteAllText(Path.Combine(root, "movie-featurette.mkv"), "c");
            File.WriteAllText(Path.Combine(nested, "nested-trailer.mkv"), "d");

            var service = new BonusProcessingService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<BonusProcessingService>.Instance,
                null!,
                null!,
                null!,
                null!);

            var paths = service.GetBonusMkvPaths(root);

            Assert.Equal(2, paths.Count);
            Assert.Contains(paths, path => path.EndsWith("movie-trailer.mkv", StringComparison.Ordinal));
            Assert.Contains(paths, path => path.EndsWith("movie-featurette.mkv", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
