using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.SeriesProcessing;

public class SeriesProcessingServiceCmdletIOTests
{
    [Fact]
    public void InvokeSeasonScan_WithoutUrls_WritesErrorViaICmdletIO()
    {
        var io = new FakeCmdletIO();
        var service = new SeriesProcessingService(
            NullLogger<SeriesProcessingService>.Instance,
            Mock.Of<IMediaReaderService>(),
            Mock.Of<IExecutableService>());

        var episodes = service.InvokeSeasonScan(io, season: 1, tvDbSeriesUrl: null, tvDbSeasonUrl: null);

        Assert.Empty(episodes);
        var error = Assert.Single(io.Errors);
        Assert.Equal(ErrorCategory.InvalidArgument, error.CategoryInfo.Category);
        Assert.Contains("TvDbUrlMissing", error.FullyQualifiedErrorId, StringComparison.Ordinal);
    }

    [Fact]
    public void NewProcessingDirectoryStructure_CreatesDirectoriesUsingPathContext()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS-CmdletIO-" + Guid.NewGuid().ToString("N"));
        try
        {
            var io = new FakeCmdletIO();
            io.Paths.CurrentLocationPath = root;
            var service = new SeriesProcessingService(
                NullLogger<SeriesProcessingService>.Instance,
                Mock.Of<IMediaReaderService>(),
                Mock.Of<IExecutableService>());

            var structure = service.NewProcessingDirectoryStructure(io, "Test Show", 2, subDirectories: ["Bonus"]);

            Assert.True(Directory.Exists(structure.RootDir));
            Assert.True(Directory.Exists(structure.SeasonDir));
            Assert.Contains("Season 02", structure.SeasonDir, StringComparison.Ordinal);
            Assert.Single(structure.SubDirs);
            Assert.True(Directory.Exists(structure.SubDirs[0]));
            Assert.Empty(io.Errors);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
