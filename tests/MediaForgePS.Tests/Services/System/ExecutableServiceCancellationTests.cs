using System;
using System.IO;
using System.Linq;
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
        new(NullLogger<ExecutableService>.Instance);

    private static string GetSleepCommand() =>
        OperatingSystem.IsWindows() ? "ping" : "sleep";

    private static string[] GetSleepArguments(int seconds) =>
        OperatingSystem.IsWindows()
            ? ["-n", (seconds + 1).ToString(), "127.0.0.1"]
            : [seconds.ToString()];
}

public class ExecutableServiceArgumentListTests
{
    [Fact]
    public void CreateProcessStartInfo_UsesArgumentListNotArgumentsString()
    {
        var args = new[] { "-i", @"C:\My Videos\show.mkv", "out.mp4" };

        var startInfo = ExecutableService.CreateProcessStartInfo("ffmpeg", args);

        Assert.Equal("ffmpeg", startInfo.FileName);
        Assert.Equal(args, startInfo.ArgumentList.ToArray());
        // ArgumentList path leaves Arguments empty until the process is started.
        Assert.True(string.IsNullOrEmpty(startInfo.Arguments));
    }

    [Fact]
    public void CreateProcessStartInfo_PreservesEmptyAndQuotedLookingArgumentsAsLiteralValues()
    {
        var args = new[] { string.Empty, "\"already quoted\"", @"path\ending\\" };

        var startInfo = ExecutableService.CreateProcessStartInfo("tool", args);

        Assert.Equal(args, startInfo.ArgumentList.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_PassesArgumentWithSpacesAsSingleArgvEntry()
    {
        var valueWithSpaces = "hello world";
        await using var script = await TempPwshScript.CreateAsync(
            "param([Parameter(Mandatory)][string]$Value)\nWrite-Output $Value",
            TestContext.Current.CancellationToken);

        var service = new ExecutableService(NullLogger<ExecutableService>.Instance);
        var result = await service.ExecuteAsync(
            "pwsh",
            ["-NoProfile", "-File", script.Path, valueWithSpaces],
            TestContext.Current.CancellationToken);

        Assert.Null(result.Exception);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(valueWithSpaces, result.Output?.Trim());
    }

    [Fact]
    public async Task ExecuteAsync_PassesFilePathWithSpacesAsSingleArgvEntry()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "MediaForge ArgumentList Test " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var filePath = Path.Combine(tempRoot, "sample file.txt");
            await File.WriteAllTextAsync(filePath, "payload", TestContext.Current.CancellationToken);

            await using var script = await TempPwshScript.CreateAsync(
                "param([Parameter(Mandatory)][string]$Path)\nGet-Content -LiteralPath $Path",
                TestContext.Current.CancellationToken);

            var service = new ExecutableService(NullLogger<ExecutableService>.Instance);
            var result = await service.ExecuteAsync(
                "pwsh",
                ["-NoProfile", "-File", script.Path, filePath],
                TestContext.Current.CancellationToken);

            Assert.Null(result.Exception);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("payload", result.Output?.Trim());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class TempPwshScript : IAsyncDisposable
    {
        private TempPwshScript(string path) => Path = path;

        public string Path { get; }

        public static async Task<TempPwshScript> CreateAsync(string contents, CancellationToken cancellationToken)
        {
            var path = global::System.IO.Path.Combine(
                global::System.IO.Path.GetTempPath(),
                "MediaForge-argv-" + Guid.NewGuid().ToString("N") + ".ps1");
            await File.WriteAllTextAsync(path, contents, cancellationToken);
            return new TempPwshScript(path);
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(Path))
                File.Delete(Path);
            return ValueTask.CompletedTask;
        }
    }
}
