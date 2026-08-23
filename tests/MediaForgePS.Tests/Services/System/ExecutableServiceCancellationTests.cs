using System;
using System.Collections.Generic;
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
        await using var script = await TempPwshScript.CreateAsync(
            "Write-Output 'ready'\nStart-Sleep -Seconds 60",
            TestContext.Current.CancellationToken);

        var service = CreateService();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var executeTask = service.ExecuteAsync(
            "pwsh",
            ["-NoProfile", "-File", script.Path],
            line =>
            {
                if (line.Contains("ready", StringComparison.OrdinalIgnoreCase))
                    started.TrySetResult();
            },
            cts.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.False(executeTask.IsCompleted);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executeTask);
        Assert.True(executeTask.IsCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeoutElapses_ThrowsTimeoutExceptionAndKillsProcess()
    {
        await using var script = await TempPwshScript.CreateAsync(
            "Write-Output 'ready'\nStart-Sleep -Seconds 60",
            TestContext.Current.CancellationToken);

        var service = CreateService();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var executeTask = service.ExecuteAsync(
            "pwsh",
            ["-NoProfile", "-File", script.Path],
            line =>
            {
                if (line.Contains("ready", StringComparison.OrdinalIgnoreCase))
                    started.TrySetResult();
            },
            TestContext.Current.CancellationToken,
            timeout: TimeSpan.FromMilliseconds(500));

        await started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => executeTask);
        Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsBeforeTimeout_ThrowsOperationCanceledException()
    {
        await using var script = await TempPwshScript.CreateAsync(
            "Write-Output 'ready'\nStart-Sleep -Seconds 60",
            TestContext.Current.CancellationToken);

        var service = CreateService();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var executeTask = service.ExecuteAsync(
            "pwsh",
            ["-NoProfile", "-File", script.Path],
            line =>
            {
                if (line.Contains("ready", StringComparison.OrdinalIgnoreCase))
                    started.TrySetResult();
            },
            cts.Token,
            timeout: TimeSpan.FromMinutes(5));

        await started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executeTask);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeoutIsNonPositive_ThrowsArgumentOutOfRangeException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ExecuteAsync("pwsh", ["-NoProfile", "-Command", "1"], TestContext.Current.CancellationToken, TimeSpan.Zero));
    }

    private static ExecutableService CreateService() =>
        new(NullLogger<ExecutableService>.Instance);

    private static string GetSleepCommand() =>
        OperatingSystem.IsWindows() ? "ping" : "sleep";

    private static string[] GetSleepArguments(int seconds) =>
        OperatingSystem.IsWindows()
            ? ["-n", (seconds + 1).ToString(), "127.0.0.1"]
            : [seconds.ToString()];

    private sealed class TempPwshScript : IAsyncDisposable
    {
        private TempPwshScript(string path) => Path = path;

        public string Path { get; }

        public static async Task<TempPwshScript> CreateAsync(string contents, CancellationToken cancellationToken)
        {
            var path = global::System.IO.Path.Combine(
                global::System.IO.Path.GetTempPath(),
                "MediaForge-cancel-" + Guid.NewGuid().ToString("N") + ".ps1");
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

public class ExecutableServiceArgumentListTests
{
    [Fact]
    public void CreateProcessStartInfo_RedirectsStandardInputOutputAndError()
    {
        var startInfo = ExecutableService.CreateProcessStartInfo("ffmpeg", ["-version"]);

        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
    }

    [Fact]
    public async Task ExecuteAsync_ClosesStandardInputSoProcessDoesNotBlockWaitingForInput()
    {
        // Reads one line from stdin then exits. With stdin redirected and closed after start,
        // Read-Host / Console.ReadLine should see EOF immediately instead of hanging.
        await using var script = await TempPwshScript.CreateAsync(
            "$line = [Console]::In.ReadLine(); if ($null -eq $line) { Write-Output 'eof'; exit 0 } else { Write-Output $line; exit 1 }",
            TestContext.Current.CancellationToken);

        var service = new ExecutableService(NullLogger<ExecutableService>.Instance);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        var result = await service.ExecuteAsync(
            "pwsh",
            ["-NoProfile", "-File", script.Path],
            cts.Token);

        Assert.Null(result.Exception);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("eof", result.Output?.Trim());
    }

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

    [Fact]
    public async Task ExecuteAsync_WithStdoutCallback_InvokesCallbackAndDoesNotRetainStdout()
    {
        await using var script = await TempPwshScript.CreateAsync(
            "Write-Output 'line-one'\nWrite-Output 'line-two'",
            TestContext.Current.CancellationToken);

        var lines = new List<string>();
        var service = new ExecutableService(NullLogger<ExecutableService>.Instance);
        var result = await service.ExecuteAsync(
            "pwsh",
            ["-NoProfile", "-File", script.Path],
            line => lines.Add(line),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Exception);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["line-one", "line-two"], lines);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CapsRetainedStderr()
    {
        var oversized = new string('e', ExecutableService.MaxSuccessErrorOutputChars + 2_000);
        await using var script = await TempPwshScript.CreateAsync(
            $"[Console]::Error.Write('{oversized}'); exit 0",
            TestContext.Current.CancellationToken);

        var service = new ExecutableService(NullLogger<ExecutableService>.Instance);
        var result = await service.ExecuteAsync(
            "pwsh",
            ["-NoProfile", "-File", script.Path],
            TestContext.Current.CancellationToken);

        Assert.Null(result.Exception);
        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.ErrorOutput);
        Assert.True(result.ErrorOutput.Length <= ExecutableService.MaxSuccessErrorOutputChars);
        Assert.EndsWith(new string('e', 32), result.ErrorOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_OnFailure_KeepsFullStderr()
    {
        var oversized = new string('f', ExecutableService.MaxSuccessErrorOutputChars + 2_000);
        await using var script = await TempPwshScript.CreateAsync(
            $"[Console]::Error.Write('{oversized}'); exit 7",
            TestContext.Current.CancellationToken);

        var service = new ExecutableService(NullLogger<ExecutableService>.Instance);
        var result = await service.ExecuteAsync(
            "pwsh",
            ["-NoProfile", "-File", script.Path],
            TestContext.Current.CancellationToken);

        Assert.Null(result.Exception);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal(oversized, result.ErrorOutput);
    }

    [Theory]
    [InlineData(null, 10, null)]
    [InlineData("short", 10, "short")]
    [InlineData("abcdefghij", 10, "abcdefghij")]
    public void TruncateTail_WhenWithinLimit_ReturnsOriginal(string? value, int maxChars, string? expected)
        => Assert.Equal(expected, ExecutableService.TruncateTail(value, maxChars));

    [Fact]
    public void TruncateTail_WhenOverLimit_KeepsTrailingPortionWithPrefix()
    {
        var value = new string('a', 20) + "TAIL";
        var truncated = ExecutableService.TruncateTail(value, 12);

        Assert.NotNull(truncated);
        Assert.Equal(12, truncated.Length);
        Assert.StartsWith("...\n", truncated, StringComparison.Ordinal);
        Assert.EndsWith("TAIL", truncated, StringComparison.Ordinal);
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
