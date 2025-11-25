using Dadstart.Labs.MediaForge.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Logging;

public class PowerShellLoggerProviderTests
{
    [Fact]
    public void CreateLogger_WithCategoryName_ReturnsLogger()
    {
        // Arrange
        var contextAccessor = new Mock<IPowerShellCommandContextAccessor>();
        var provider = new PowerShellLoggerProvider(contextAccessor.Object);
        var categoryName = "TestCategory";

        // Act
        var logger = provider.CreateLogger(categoryName);

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void CreateLogger_WithDifferentCategoryNames_ReturnsDifferentLoggers()
    {
        // Arrange
        var contextAccessor = new Mock<IPowerShellCommandContextAccessor>();
        var provider = new PowerShellLoggerProvider(contextAccessor.Object);

        // Act
        var logger1 = provider.CreateLogger("Category1");
        var logger2 = provider.CreateLogger("Category2");

        // Assert
        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
        Assert.NotEqual(logger1, logger2);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var contextAccessor = new Mock<IPowerShellCommandContextAccessor>();
        var provider = new PowerShellLoggerProvider(contextAccessor.Object);

        // Act & Assert
        provider.Dispose();
    }
}

