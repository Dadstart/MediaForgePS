using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.System;

public class ExecutableService : IExecutableService
{
    private readonly ILogger<ExecutableService> _logger;

    public ExecutableService(ILogger<ExecutableService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExecutableResult> ExecuteAsync(
        string command,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        return await ExecuteAsyncInternal(command, arguments, null, cancellationToken, timeout).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ExecutableResult> ExecuteAsync(
        string command,
        IEnumerable<string> arguments,
        Action<string> stdoutCallback,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(stdoutCallback);
        return await ExecuteAsyncInternal(command, arguments, stdoutCallback, cancellationToken, timeout).ConfigureAwait(false);
    }

    private async Task<ExecutableResult> ExecuteAsyncInternal(
        string command,
        IEnumerable<string> arguments,
        Action<string>? stdoutCallback,
        CancellationToken cancellationToken,
        TimeSpan? timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);
        if (timeout is { } invalidTimeout && invalidTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive when specified.");

        cancellationToken.ThrowIfCancellationRequested();

        // Materialize once so ArgumentList and logs share the same argv values (no shell quoting).
        var argumentList = arguments as IReadOnlyList<string> ?? arguments.ToList();
        var argumentsForLog = string.Join(' ', argumentList);

        if (!OperatingSystem.IsWindows() && WindowsExecutablePathHelper.IsWindowsExecutableCommand(command))
        {
            var message = WindowsExecutablePathHelper.FormatWindowsExecutableUnsupportedMessage(command);
            _logger.LogWarning("{Message}", message);
            return new ExecutableResult(null, null, null, new PlatformNotSupportedException(message));
        }

        var logMessage = stdoutCallback != null
            ? "Executing command with streaming stdout: {Command} with arguments: {Arguments}"
            : "Executing command: {Command} with arguments: {Arguments}";
        _logger.LogDebug(logMessage, command, argumentsForLog);

        Process? process = null;
        CancellationTokenSource? timeoutCts = null;
        try
        {
            var linkedToken = cancellationToken;
            if (timeout is { } timeoutValue)
            {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeoutValue);
                linkedToken = timeoutCts.Token;
                _logger.LogDebug("Process timeout set to {Timeout} for command: {Command}", timeoutValue, command);
            }

            process = CreateAndStartProcess(command, argumentList);
            _logger.LogTrace("Process started successfully. Process ID: {ProcessId}", process.Id);

            using var registration = linkedToken.Register(
                static state => TryKillProcessTree((Process)state!),
                process);

            linkedToken.ThrowIfCancellationRequested();

            var (stdout, stderr) = await ReadProcessOutputAsync(process, stdoutCallback, linkedToken).ConfigureAwait(false);

            // Killing the process on cancel/timeout can make WaitForExit complete normally;
            // always surface cancellation/timeout instead of treating it as a failed exit.
            if (linkedToken.IsCancellationRequested)
                ThrowIfCanceledOrTimedOut(cancellationToken, timeout, command);

            return CreateResult(process, stdout, stderr, command);
        }
        catch (OperationCanceledException ex)
        {
            TryKillProcessTree(process);
            ThrowIfCanceledOrTimedOut(cancellationToken, timeout, command, ex);
            _logger.LogWarning("Command execution was cancelled: {Command}", command);
            throw;
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while executing command: {Command} with arguments: {Arguments}", command, argumentsForLog);
            return new ExecutableResult(null, null, null, ex);
        }
        finally
        {
            timeoutCts?.Dispose();
            process?.Dispose();
        }
    }

    private void ThrowIfCanceledOrTimedOut(
        CancellationToken cancellationToken,
        TimeSpan? timeout,
        string command,
        OperationCanceledException? cancelException = null)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Command execution was cancelled: {Command}", command);
            if (cancelException is not null)
                throw cancelException;
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (timeout is { } timedOut)
        {
            _logger.LogWarning("Command execution timed out after {Timeout}: {Command}", timedOut, command);
            throw new TimeoutException($"Command '{command}' timed out after {timedOut}.", cancelException);
        }

        if (cancelException is not null)
            throw cancelException;
    }

    /// <summary>
    /// Builds process start info using <see cref="ProcessStartInfo.ArgumentList"/> so the runtime
    /// applies platform-correct quoting instead of a hand-joined Arguments string.
    /// </summary>
    internal static ProcessStartInfo CreateProcessStartInfo(string command, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            // Close stdin after start so tools like FFmpeg do not hang waiting for interactive input.
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            processStartInfo.ArgumentList.Add(argument);

        return processStartInfo;
    }

    private Process CreateAndStartProcess(string command, IReadOnlyList<string> arguments)
    {
        var processStartInfo = CreateProcessStartInfo(command, arguments);
        var process = new Process { StartInfo = processStartInfo };

        if (!process.Start())
        {
            var argumentsForError = string.Join(' ', arguments);
            var errorMessage = $"Failed to start process '{command}' with arguments: {argumentsForError}";
            _logger.LogError(errorMessage);
            process.Dispose();
            throw new InvalidOperationException(errorMessage);
        }

        // Signal EOF on stdin immediately. Without this, FFmpeg (and similar tools) can block
        // indefinitely waiting for console input when stdin is attached to a redirected pipe.
        try
        {
            process.StandardInput.Close();
        }
        catch (Exception ex) when (
            ex is ObjectDisposedException
                or InvalidOperationException
                or IOException)
        {
            _logger.LogTrace(ex, "Failed to close redirected stdin for process '{Command}'", command);
        }

        return process;
    }

    private static void TryKillProcessTree(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (process.HasExited)
                return;

            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or NotSupportedException
                or global::System.ComponentModel.Win32Exception)
        {
            // Best effort: process may already be exiting or unsupported on the host.
        }
    }

    private async Task<(string? stdout, string? stderr)> ReadProcessOutputAsync(Process process, Action<string>? stdoutCallback, CancellationToken cancellationToken)
    {
        Task<string> stdoutTask;
        if (stdoutCallback != null)
        {
            // Read stdout line-by-line with callback
            var stdoutLines = new List<string>();
            stdoutTask = Task.Run(async () =>
            {
                using var reader = process.StandardOutput;
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                {
                    stdoutLines.Add(line);
                    try
                    {
                        stdoutCallback(line);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Exception in stdout callback for command: {Command}", process.StartInfo.FileName);
                    }
                }
                return string.Join(Environment.NewLine, stdoutLines);
            }, cancellationToken);
        }
        else
        {
            // Read stdout all at once
            stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        }

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return (stdout, stderr);
    }

    private ExecutableResult CreateResult(Process process, string? stdout, string? stderr, string command)
    {
        _logger.LogDebug(
            "Process completed. Exit code: {ExitCode}, StdOut length: {StdOutLength}, StdErr length: {StdErrLength}",
            process.ExitCode,
            stdout?.Length ?? 0,
            stderr?.Length ?? 0);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "Process exited with non-zero code. Exit code: {ExitCode}, StdErr: {StdErr}",
                process.ExitCode,
                stderr);
        }

        if (!string.IsNullOrEmpty(stderr))
        {
            _logger.LogTrace("Process stderr output: {StdErr}", stderr);
        }

        return new ExecutableResult(stdout, stderr, process.ExitCode);
    }
}
