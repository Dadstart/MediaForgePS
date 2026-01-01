namespace Dadstart.Labs.MediaForge.Services.System;

public interface IExecutableService
{
    /// <summary>
    /// Executes a command with the given arguments and returns the result.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">The arguments to pass to the command.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the command execution.</returns>
    Task<ExecutableResult> ExecuteAsync(string command, IEnumerable<string> arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a command with the given arguments and streams stdout output line-by-line via a callback.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">The arguments to pass to the command.</param>
    /// <param name="stdoutCallback">Callback invoked for each line of stdout output.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the command execution.</returns>
    Task<ExecutableResult> ExecuteAsync(string command, IEnumerable<string> arguments, Action<string> stdoutCallback, CancellationToken cancellationToken = default);
}
