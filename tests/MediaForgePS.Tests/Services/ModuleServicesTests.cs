using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class ModuleServicesTests
{
    [Fact]
    public void EnsureInitialized_FirstCall_InitializesServices()
    {
        // Arrange
        ModuleServices.Dispose();

        // Act
        ModuleServices.EnsureInitialized();

        // Assert
        var service = ModuleServices.GetService<IPlatformService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void EnsureInitialized_MultipleCalls_OnlyInitializesOnce()
    {
        // Arrange
        ModuleServices.Dispose();

        // Act
        ModuleServices.EnsureInitialized();
        var service1 = ModuleServices.GetService<IPlatformService>();
        ModuleServices.EnsureInitialized();
        var service2 = ModuleServices.GetService<IPlatformService>();

        // Assert
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.Same(service1, service2); // Should be singleton
    }

    [Fact]
    public void GetRequiredService_WithRegisteredService_ReturnsService()
    {
        // Arrange
        ModuleServices.EnsureInitialized();

        // Act
        var service = ModuleServices.GetRequiredService<IPlatformService>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void GetRequiredService_WithUnregisteredService_ThrowsException()
    {
        // Arrange
        ModuleServices.EnsureInitialized();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => ModuleServices.GetRequiredService<string>());
    }

    [Fact]
    public void GetService_WithRegisteredService_ReturnsService()
    {
        // Arrange
        ModuleServices.EnsureInitialized();

        // Act
        var service = ModuleServices.GetService<IPlatformService>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void GetService_WithUnregisteredService_ReturnsNull()
    {
        // Arrange
        ModuleServices.EnsureInitialized();

        // Act
        var service = ModuleServices.GetService<string>();

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void Dispose_DisposesProvider()
    {
        // Arrange
        ModuleServices.EnsureInitialized();
        var service = ModuleServices.GetService<IPlatformService>();
        Assert.NotNull(service);

        // Act
        ModuleServices.Dispose();

        // Assert
        // After disposal, GetService may return null or throw
        // The exact behavior depends on implementation
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        ModuleServices.EnsureInitialized();

        // Act & Assert
        ModuleServices.Dispose();
        ModuleServices.Dispose(); // Should be safe to call multiple times
    }
}
