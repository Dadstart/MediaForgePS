using System;
using System.Diagnostics;

namespace Dadstart.Labs.MediaForge.Services.System;

public class ExecutableService : IExecutableService
{
    /// <inheritdoc />
    public async Task<ExecutableResult> Execute(string command, IEnumerable<string> arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo()
            {
                FileName = command,
                Arguments = arguments.ToQuotedArgumentString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process() { StartInfo = processStartInfo };

            if (!process.Start())
                throw new InvalidOperationException($"Failed to start process {command} with arguments {string.Join(" ", arguments)}");

            // read both streams asynchronously to prevent deadlocks
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            return new ExecutableResult(await stdoutTask, await stderrTask, process.ExitCode);

        }
        catch (Exception exc)
        {
            return new ExecutableResult(null, null, null, exc);
        }
    }
}