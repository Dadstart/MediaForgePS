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
    public async Task<ExecutableResult> Execute(string command, IEnumerable<string> arguments)
    {
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
                var errorMessage = $"Failed to start process {command} with arguments {argumentsString}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogTrace("Process started successfully. Process ID: {ProcessId}", process.Id);

            // read both streams asynchronously to prevent deadlocks
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync().ConfigureAwait(false);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while executing command: {Command} with arguments: {Arguments}", command, argumentsString);
            return new ExecutableResult(null, null, null, ex);
        }
    }
}
