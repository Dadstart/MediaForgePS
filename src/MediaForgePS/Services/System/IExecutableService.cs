namespace Dadstart.Labs.MediaForge.Services.System;

public interface IExecutableService
{
    /// <summary>
    /// Executes a command with the given arguments and returns the result.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">The arguments to pass to the command.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation (e.g. StoppingToken).</param>
    /// <param name="timeout">
    /// Optional wall-clock timeout linked with <paramref name="cancellationToken"/>.
    /// When both are set, whichever fires first cancels the process.
    /// Use <see cref="ProcessTimeouts"/> for recommended durations.
    /// </param>
    /// <returns>The result of the command execution.</returns>
    /// <exception cref="TimeoutException">Thrown when <paramref name="timeout"/> elapses before the process exits.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    Task<ExecutableResult> ExecuteAsync(
        string command,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null);

    /// <summary>
    /// Executes a command with the given arguments and streams stdout output line-by-line via a callback.
    /// Streamed stdout is not retained on the returned <see cref="ExecutableResult"/> (discarded after the callback).
    /// On success, retained stderr is capped; failures keep full stderr for diagnostics.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">The arguments to pass to the command.</param>
    /// <param name="stdoutCallback">Callback invoked for each line of stdout output.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation (e.g. StoppingToken).</param>
    /// <param name="timeout">
    /// Optional wall-clock timeout linked with <paramref name="cancellationToken"/>.
    /// When both are set, whichever fires first cancels the process.
    /// Use <see cref="ProcessTimeouts"/> for recommended durations.
    /// </param>
    /// <returns>The result of the command execution. <see cref="ExecutableResult.Output"/> is null when streaming.</returns>
    /// <exception cref="TimeoutException">Thrown when <paramref name="timeout"/> elapses before the process exits.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    Task<ExecutableResult> ExecuteAsync(
        string command,
        IEnumerable<string> arguments,
        Action<string> stdoutCallback,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null);
}
