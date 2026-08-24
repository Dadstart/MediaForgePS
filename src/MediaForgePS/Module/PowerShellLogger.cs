using System;
using System.Management.Automation;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Module;

/// <summary>
/// ILogger implementation that forwards log messages into the current PSCmdlet streams.
/// If no cmdlet is present in CmdletContext.Current, messages are ignored.
/// Error and Critical map to WriteWarning so routine service logging does not produce
/// pipeline error records; cmdlets should call WriteError for intentional failures.
/// Analyzer MFPS001 enforces this for catch blocks that call Logger.LogError.
/// </summary>
public class PowerShellLogger : ILogger
{
    private readonly string _category;

    public PowerShellLogger(string category)
    {
        _category = category;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullDisposable.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        // Let the logging pipeline decide; keep this simple and always enabled.
        return true;
    }

    public void Log<TState>(LogLevel level, EventId _,
        TState state, Exception? exc, Func<TState, Exception?, string> formatter)
    {
        try
        {
            LogCore(level, state, exc, formatter);
        }
        catch
        {
            // Exceptions during logging are ignored to prevent logging failures from breaking application flow
        }
    }

    private void LogCore<TState>(LogLevel level,
        TState state, Exception? exc, Func<TState, Exception?, string> formatter)
    {
        if (formatter is null)
            return;

        var msg = formatter(state, exc);
        if (string.IsNullOrEmpty(msg) && exc is null) return;

        var cmdlet = CmdletContext.Current;
        if (cmdlet == null)
        {
            // No cmdlet context: drop
            return;
        }

        // Prepend category to help identify source
        var output = string.IsNullOrEmpty(_category) ? msg : $"[{_category}] {msg}";

        switch (level)
        {
            case LogLevel.Trace:
                cmdlet.WriteVerbose(output);
                break;
            case LogLevel.Debug:
                cmdlet.WriteDebug(output);
                break;
            case LogLevel.Information:
                var infoRecord = new InformationRecord(output, _category);
                cmdlet.WriteInformation(infoRecord);
                break;
            case LogLevel.Warning:
            case LogLevel.Error:
            case LogLevel.Critical:
                if (exc is not null)
                    output = string.IsNullOrEmpty(msg) ? FormatCategory(exc.ToString()) : $"{output}{Environment.NewLine}{exc}";
                cmdlet.WriteWarning(output);
                break;

            default:
                cmdlet.WriteVerbose(output);
                break;
        }
    }

    private string FormatCategory(string message) =>
        string.IsNullOrEmpty(_category) ? message : $"[{_category}] {message}";

    private class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
