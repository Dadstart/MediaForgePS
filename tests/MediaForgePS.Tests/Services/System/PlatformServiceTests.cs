using Dadstart.Labs.MediaForge.Services.System;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class PlatformServiceTests
{
    [Fact]
    public void IsWindows_ReturnsCorrectValue()
    {
        // Arrange
        var service = new PlatformService();

        // Act
        var isWindows = service.IsWindows();

        // Assert
        // This will vary by platform, but should match OperatingSystem.IsWindows()
        Assert.Equal(OperatingSystem.IsWindows(), isWindows);
    }
}
