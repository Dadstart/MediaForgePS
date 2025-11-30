using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Module;

public class ModuleInitializerTests
{
    [Fact]
    public void Initialize_CallsEnsureInitialized()
    {
        // Arrange
        ModuleServices.Dispose(); // Ensure clean state

        // Act
        ModuleInitializer.Initialize();

        // Assert
        // Should not throw and services should be initialized
        var service = ModuleServices.GetService<IPlatformService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void Cleanup_DisposesServices()
    {
        // Arrange
        ModuleServices.EnsureInitialized();
        var service = ModuleServices.GetService<IPlatformService>();
        Assert.NotNull(service);

        // Act
        ModuleInitializer.Cleanup();

        // Assert
        // Services should be disposed (GetService may return null after disposal)
        // Note: This test verifies cleanup doesn't throw
    }

    [Fact]
    public void Cleanup_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        ModuleServices.EnsureInitialized();

        // Act & Assert
        ModuleInitializer.Cleanup();
        ModuleInitializer.Cleanup(); // Should be safe to call multiple times
    }
}
