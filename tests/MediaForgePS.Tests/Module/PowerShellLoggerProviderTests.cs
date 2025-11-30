using Dadstart.Labs.MediaForge.Module;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Module;

public class PowerShellLoggerProviderTests
{
    [Fact]
    public void CreateLogger_WithCategoryName_ReturnsPowerShellLogger()
    {
        // Arrange
        var provider = new PowerShellLoggerProvider();
        var categoryName = "TestCategory";

        // Act
        var logger = provider.CreateLogger(categoryName);

        // Assert
        Assert.NotNull(logger);
        Assert.IsType<PowerShellLogger>(logger);
    }

    [Fact]
    public void CreateLogger_WithDifferentCategoryNames_ReturnsDifferentInstances()
    {
        // Arrange
        var provider = new PowerShellLoggerProvider();

        // Act
        var logger1 = provider.CreateLogger("Category1");
        var logger2 = provider.CreateLogger("Category2");

        // Assert
        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
        Assert.NotSame(logger1, logger2);
    }

    [Fact]
    public void CreateLogger_WithSameCategoryName_ReturnsNewInstance()
    {
        // Arrange
        var provider = new PowerShellLoggerProvider();
        var categoryName = "TestCategory";

        // Act
        var logger1 = provider.CreateLogger(categoryName);
        var logger2 = provider.CreateLogger(categoryName);

        // Assert
        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
        Assert.NotSame(logger1, logger2);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var provider = new PowerShellLoggerProvider();

        // Act & Assert
        provider.Dispose();
        provider.Dispose(); // Should be safe to call multiple times
    }
}
