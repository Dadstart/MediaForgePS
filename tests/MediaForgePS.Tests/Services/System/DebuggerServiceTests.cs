using Dadstart.Labs.MediaForge.Services.System;
using System.Diagnostics;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class DebuggerServiceTests
{
    [Fact]
    public void IsDebugging_WhenNotForcedAndNoDebugger_ReturnsFalse()
    {
        // Arrange
        DebuggerService.ForceDebugging = false;
        DebuggerService.BreakAll = false;
        var service = new DebuggerService();

        // Act
        var isDebugging = service.IsDebugging;

        // Assert
        Assert.Equal(Debugger.IsAttached, isDebugging);
    }

    [Fact]
    public void IsDebugging_WhenForced_ReturnsTrue()
    {
        // Arrange
        DebuggerService.ForceDebugging = true;
        var service = new DebuggerService();

        // Act
        var isDebugging = service.IsDebugging;

        // Assert
        Assert.True(isDebugging);
    }

    [Fact]
    public void PowerShellBreakOnBeginProcessing_WhenNotSet_ReturnsFalse()
    {
        // Arrange
        DebuggerService.ForceDebugging = false;
        DebuggerService.BreakAll = false;
        var service = new DebuggerService();

        // Act
        var shouldBreak = service.PowerShellBreakOnBeginProcessing;

        // Assert
        Assert.False(shouldBreak);
    }

    [Fact]
    public void PowerShellBreakOnBeginProcessing_WhenSet_ReturnsTrueWhenDebugging()
    {
        // Arrange
        DebuggerService.ForceDebugging = true;
        DebuggerService.BreakAll = false;
        var service = new DebuggerService();
        service.PowerShellBreakOnBeginProcessing = true;

        // Act
        var shouldBreak = service.PowerShellBreakOnBeginProcessing;

        // Assert
        Assert.True(shouldBreak);
    }

    [Fact]
    public void PowerShellBreakOnBeginProcessing_WhenBreakAll_ReturnsTrueWhenDebugging()
    {
        // Arrange
        DebuggerService.ForceDebugging = true;
        DebuggerService.BreakAll = true;
        var service = new DebuggerService();
        service.PowerShellBreakOnBeginProcessing = false;

        // Act
        var shouldBreak = service.PowerShellBreakOnBeginProcessing;

        // Assert
        Assert.True(shouldBreak);
    }

    [Fact]
    public void PowerShellBreakOnProcessRecord_WhenNotSet_ReturnsFalse()
    {
        // Arrange
        DebuggerService.ForceDebugging = false;
        DebuggerService.BreakAll = false;
        var service = new DebuggerService();

        // Act
        var shouldBreak = service.PowerShellBreakOnProcessRecord;

        // Assert
        Assert.False(shouldBreak);
    }

    [Fact]
    public void PowerShellBreakOnProcessRecord_WhenSet_ReturnsTrueWhenDebugging()
    {
        // Arrange
        DebuggerService.ForceDebugging = true;
        DebuggerService.BreakAll = false;
        var service = new DebuggerService();
        service.PowerShellBreakOnProcessRecord = true;

        // Act
        var shouldBreak = service.PowerShellBreakOnProcessRecord;

        // Assert
        Assert.True(shouldBreak);
    }

    [Fact]
    public void PowerShellBreakOnEndProcessing_WhenNotSet_ReturnsFalse()
    {
        // Arrange
        DebuggerService.ForceDebugging = false;
        DebuggerService.BreakAll = false;
        var service = new DebuggerService();

        // Act
        var shouldBreak = service.PowerShellBreakOnEndProcessing;

        // Assert
        Assert.False(shouldBreak);
    }

    [Fact]
    public void PowerShellBreakOnEndProcessing_WhenSet_ReturnsTrueWhenDebugging()
    {
        // Arrange
        DebuggerService.ForceDebugging = true;
        DebuggerService.BreakAll = false;
        var service = new DebuggerService();
        service.PowerShellBreakOnEndProcessing = true;

        // Act
        var shouldBreak = service.PowerShellBreakOnEndProcessing;

        // Assert
        Assert.True(shouldBreak);
    }

    [Fact]
    public void BreakIfDebugging_WithFalse_DoesNotBreak()
    {
        // Arrange
        var service = new DebuggerService();

        // Act & Assert
        service.BreakIfDebugging(false);
        // Should not throw or break
    }

    [Fact]
    public void BreakIfDebugging_WithTrue_MayBreak()
    {
        // Arrange
        var service = new DebuggerService();

        // Act & Assert
        // Note: This will only break if a debugger is attached
        // We can't easily test the actual break behavior, but we can verify it doesn't throw
        service.BreakIfDebugging(true);
    }
}
