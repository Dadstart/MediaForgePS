using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public async Task<ExecutableResult> ExecuteAsync(string command, IEnumerable<string> arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsyncInternal(command, arguments, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ExecutableResult> ExecuteAsync(string command, IEnumerable<string> arguments, Action<string> stdoutCallback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stdoutCallback);
        return await ExecuteAsyncInternal(command, arguments, stdoutCallback, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ExecutableResult> ExecuteAsyncInternal(string command, IEnumerable<string> arguments, Action<string>? stdoutCallback, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        cancellationToken.ThrowIfCancellationRequested();

        // Materialize once so ArgumentList and logs share the same argv values (no shell quoting).
        var argumentList = arguments as IReadOnlyList<string> ?? arguments.ToList();
        var argumentsForLog = string.Join(' ', argumentList);
        var logMessage = stdoutCallback != null
            ? "Executing command with streaming stdout: {Command} with arguments: {Arguments}"
            : "Executing command: {Command} with arguments: {Arguments}";
        _logger.LogDebug(logMessage, command, argumentsForLog);

        Process? process = null;
        try
        {
            process = CreateAndStartProcess(command, argumentList);
            _logger.LogTrace("Process started successfully. Process ID: {ProcessId}", process.Id);

            using var registration = cancellationToken.Register(
                static state => TryKillProcessTree((Process)state!),
                process);

            cancellationToken.ThrowIfCancellationRequested();

            var (stdout, stderr) = await ReadProcessOutputAsync(process, stdoutCallback, cancellationToken).ConfigureAwait(false);

            // Killing the process on cancel can make WaitForExit complete normally;
            // always surface cancellation instead of treating it as a failed exit.
            cancellationToken.ThrowIfCancellationRequested();

            return CreateResult(process, stdout, stderr, command);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            _logger.LogWarning("Command execution was cancelled: {Command}", command);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while executing command: {Command} with arguments: {Arguments}", command, argumentsForLog);
            return new ExecutableResult(null, null, null, ex);
        }
        finally
        {
            process?.Dispose();
        }
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
