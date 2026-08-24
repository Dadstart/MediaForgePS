using System;
using System.IO;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.BonusProcessing;

public class BonusOrganizationPhaseTests
{
    [Fact]
    public void GetFileSizeOrZero_ReturnsExpectedSize_WhenFileExists()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "MediaForgePS-BonusOrg-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(tempPath, "12345");

        try
        {
            var size = BonusOrganizationPhase.GetFileSizeOrZero(tempPath);
            Assert.Equal(5, size);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void GetFileSizeOrZero_ReturnsZero_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS-BonusOrg-Missing-" + Guid.NewGuid().ToString("N") + ".txt");
        Assert.Equal(0, BonusOrganizationPhase.GetFileSizeOrZero(path));
    }
}
