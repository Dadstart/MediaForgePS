using System;

namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Interprets <see cref="ExecutableResult"/> consistently across FFmpeg and Subtitle Edit callers.
/// </summary>
public static class ExecutableResultExtensions
{
    /// <summary>
    /// Throws when the result carries an infrastructure <see cref="ExecutableResult.Exception"/>.
    /// </summary>
    public static void ThrowIfInfrastructureFailure(this ExecutableResult result, string operation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        if (result.Exception is null)
            return;

        throw new InvalidOperationException(
            $"{operation} failed: {result.Exception.Message}",
            result.Exception);
    }

    /// <summary>
    /// Throws when the result carries an infrastructure exception or a non-zero process exit code.
    /// </summary>
    public static void EnsureProcessSuccess(this ExecutableResult result, string operation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        result.ThrowIfInfrastructureFailure(operation);

        if (result.ExitCode is null or 0)
            return;

        var message = $"{operation} failed with exit code {result.ExitCode.Value}";
        if (!string.IsNullOrWhiteSpace(result.ErrorOutput))
            message += ". " + result.ErrorOutput.Trim();

        throw new InvalidOperationException(message);
    }
}
