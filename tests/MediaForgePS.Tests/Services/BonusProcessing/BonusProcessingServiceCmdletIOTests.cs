using System;
using System.IO;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.BonusProcessing;

public class BonusProcessingServiceCmdletIOTests
{
    [Fact]
    public void InvokeOrganizationPhase_MovesTrailerMp4IntoPlexFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS-BonusOrgIO-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "dest");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);

        var trailerPath = Path.Combine(source, "movie-trailer.mp4");
        File.WriteAllText(trailerPath, "video");

        try
        {
            var io = new FakeCmdletIO();
            var service = new BonusProcessingService(NullLogger<BonusProcessingService>.Instance, null!, null!, null!, null!);
            var result = service.InvokeOrganizationPhase(
                io,
                new BonusOrganizationRequest(source, destination),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.FilesMoved);
            Assert.Equal(1, result.MoveCandidates);
            Assert.False(File.Exists(trailerPath));
            Assert.True(File.Exists(Path.Combine(destination, "Trailers", "movie-trailer.mp4")));
            Assert.Empty(io.Errors);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvokeOrganizationPhase_WhenDestinationMissing_ThrowsDirectoryNotFoundException()
    {
        var io = new FakeCmdletIO();
        var service = new BonusProcessingService(NullLogger<BonusProcessingService>.Instance, null!, null!, null!, null!);

        Assert.Throws<DirectoryNotFoundException>(() =>
            service.InvokeOrganizationPhase(
                io,
                new BonusOrganizationRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
                TestContext.Current.CancellationToken));
    }
}
