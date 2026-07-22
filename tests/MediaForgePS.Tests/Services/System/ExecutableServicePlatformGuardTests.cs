using System;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class ExecutableServicePlatformGuardTests
{
    [Fact]
    public async Task ExecuteAsync_WhenWindowsExeOnNonWindows_ReturnsPlatformNotSupportedAndWarns()
    {
        if (OperatingSystem.IsWindows())
            return;

        var logger = new Mock<ILogger<ExecutableService>>();
        var service = new ExecutableService(logger.Object);

        var result = await service.ExecuteAsync(
            @"C:\Program Files\mkvtoolnix\mkvextract.exe",
            ["tracks", "input.mkv", "0:out.sub"],
            TestContext.Current.CancellationToken);

        Assert.Null(result.ExitCode);
        var ex = Assert.IsType<PlatformNotSupportedException>(result.Exception);
        Assert.Contains("mkvextract.exe", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows only", ex.Message, StringComparison.OrdinalIgnoreCase);

        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("mkvextract.exe", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNonExeCommandOnNonWindows_DoesNotShortCircuitAsPlatformGuard()
    {
        if (OperatingSystem.IsWindows())
            return;

        var logger = new Mock<ILogger<ExecutableService>>();
        var service = new ExecutableService(logger.Object);

        // A missing non-.exe command should fail for process-start reasons, not the Windows .exe guard.
        var result = await service.ExecuteAsync(
            "mediaforge-definitely-missing-command",
            Array.Empty<string>(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result.Exception);
        Assert.IsNotType<PlatformNotSupportedException>(result.Exception);
    }
}

public class WindowsExecutablePathHelperTests
{
    [Fact]
    public void IsWindowsExecutableCommand_DetectsExeSuffix()
    {
        Assert.True(WindowsExecutablePathHelper.IsWindowsExecutableCommand(@"C:\tools\mkvextract.exe"));
        Assert.True(WindowsExecutablePathHelper.IsWindowsExecutableCommand("SubtitleEdit.EXE"));
        Assert.False(WindowsExecutablePathHelper.IsWindowsExecutableCommand("ffmpeg"));
        Assert.False(WindowsExecutablePathHelper.IsWindowsExecutableCommand(null));
        Assert.False(WindowsExecutablePathHelper.IsWindowsExecutableCommand(" "));
    }

    [Fact]
    public void GetMkvextractPath_OnNonWindows_ReturnsNull()
    {
        if (OperatingSystem.IsWindows())
            return;

        Assert.Null(WindowsExecutablePathHelper.GetMkvextractPath());
    }

    [Fact]
    public void FormatWindowsExecutableUnsupportedMessage_IncludesFileName()
    {
        var message = WindowsExecutablePathHelper.FormatWindowsExecutableUnsupportedMessage(
            @"C:\Program Files\mkvtoolnix\mkvextract.exe");

        Assert.Contains("mkvextract.exe", message, StringComparison.Ordinal);
        Assert.Contains("Windows only", message, StringComparison.OrdinalIgnoreCase);
    }
}
