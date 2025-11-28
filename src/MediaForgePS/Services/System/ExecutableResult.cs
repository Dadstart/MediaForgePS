namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// The result of a command execution.
/// </summary>
/// <param name="Output">The standard output of the command.</param>
/// <param name="ErrorOutput">The standard error output of the command.</param>
/// <param name="ExitCode">The exit code of the command.</param>
public record ExecutableResult(string? Output, string? ErrorOutput, int? ExitCode, Exception? Exception = null);
