namespace Dadstart.Labs.MediaForge.Services.System;

public interface IExecutableService
{
    /// <summary>
    /// Executes a command with the given arguments and returns the result.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">The arguments to pass to the command.</param>
    /// <returns>The result of the command execution.</returns>
    Task<ExecutableResult> Execute(string command, IEnumerable<string> arguments);
}