using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.System;

public class ExecutableService : IExecutableService
{
    private readonly IPlatformService _platformService;
    private readonly ILogger<ExecutableService> _logger;

    public ExecutableService(IPlatformService platformService, ILogger<ExecutableService> logger)
    {
        _platformService = platformService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExecutableResult> ExecuteAsync(string command, IEnumerable<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        cancellationToken.ThrowIfCancellationRequested();

        var argumentsString = arguments.ToQuotedArgumentString(_platformService);
        _logger.LogDebug("Executing command: {Command} with arguments: {Arguments}", command, argumentsString);

        try
        {
            var processStartInfo = new ProcessStartInfo()
            {
                FileName = command,
                Arguments = argumentsString,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process() { StartInfo = processStartInfo };

            if (!process.Start())
            {
                var errorMessage = $"Failed to start process '{command}' with arguments: {argumentsString}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogTrace("Process started successfully. Process ID: {ProcessId}", process.Id);

            cancellationToken.ThrowIfCancellationRequested();

            // read both streams asynchronously to prevent deadlocks
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

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
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Command execution was cancelled: {Command}", command);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while executing command: {Command} with arguments: {Arguments}", command, argumentsString);
            return new ExecutableResult(null, null, null, ex);
        }
    }

    /// <inheritdoc />
    public async Task<ExecutableResult> ExecuteAsync(string command, IEnumerable<string> arguments, Action<string> stdoutCallback, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(stdoutCallback);

        cancellationToken.ThrowIfCancellationRequested();

        var argumentsString = arguments.ToQuotedArgumentString(_platformService);
        _logger.LogDebug("Executing command with streaming stdout: {Command} with arguments: {Arguments}", command, argumentsString);

        try
        {
            var processStartInfo = new ProcessStartInfo()
            {
                FileName = command,
                Arguments = argumentsString,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process() { StartInfo = processStartInfo };

            if (!process.Start())
            {
                var errorMessage = $"Failed to start process '{command}' with arguments: {argumentsString}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogTrace("Process started successfully. Process ID: {ProcessId}", process.Id);

            cancellationToken.ThrowIfCancellationRequested();

            // Read stdout line-by-line with callback, stderr asynchronously
            var stdoutLines = new List<string>();
            var stdoutTask = Task.Run(async () =>
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
                        _logger.LogWarning(ex, "Exception in stdout callback for command: {Command}", command);
                    }
                }
            }, cancellationToken);

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            var stdout = string.Join(Environment.NewLine, stdoutLines);

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
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Command execution was cancelled: {Command}", command);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while executing command: {Command} with arguments: {Arguments}", command, argumentsString);
            return new ExecutableResult(null, null, null, ex);
        }
    }
}
