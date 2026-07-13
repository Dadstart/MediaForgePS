using System;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class ExecutableServiceCancellationTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTokenAlreadyCanceled_ThrowsBeforeStartingProcess()
    {
        var service = CreateService();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(GetSleepCommand(), GetSleepArguments(30), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCanceledDuringExecution_KillsProcessAndThrows()
    {
        var service = CreateService();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var executeTask = service.ExecuteAsync(GetSleepCommand(), GetSleepArguments(60), cts.Token);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.False(executeTask.IsCompleted);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executeTask);

        // Give the OS a moment to reap the killed process tree.
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.True(executeTask.IsCompleted);
    }

    private static ExecutableService CreateService() =>
        new(new PlatformService(), NullLogger<ExecutableService>.Instance);

    private static string GetSleepCommand() =>
        OperatingSystem.IsWindows() ? "ping" : "sleep";

    private static string[] GetSleepArguments(int seconds) =>
        OperatingSystem.IsWindows()
            ? ["-n", (seconds + 1).ToString(), "127.0.0.1"]
            : [seconds.ToString()];
}
