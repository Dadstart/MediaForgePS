using System;
using System.Management.Automation;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Logging;

/// <summary>
/// PowerShell-aware logger that bridges Microsoft.Extensions.Logging to PowerShell Write-* cmdlets.
/// </summary>
public class PowerShellLogger : ILogger
{
    private readonly string _categoryName;
    private readonly IPowerShellCommandContextAccessor _contextAccessor;
    private readonly Func<string, LogLevel, bool> _filter;

    public PowerShellLogger(
        string categoryName,
        IPowerShellCommandContextAccessor contextAccessor,
        Func<string, LogLevel, bool>? filter = null)
    {
        _categoryName = categoryName;
        _contextAccessor = contextAccessor;
        _filter = filter ?? ((category, level) => true);
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullDisposable.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
            return false;

        if (!_filter(_categoryName, logLevel))
            return false;

        var cmdlet = _contextAccessor.GetCurrentContext();
        if (cmdlet is null)
            return false;

        return logLevel switch
        {
            LogLevel.Trace or LogLevel.Debug => IsDebugEnabled(cmdlet),
            LogLevel.Information => IsVerboseEnabled(cmdlet),
            LogLevel.Warning or LogLevel.Error or LogLevel.Critical => true,
            _ => false
        };
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var cmdlet = _contextAccessor.GetCurrentContext();
        if (cmdlet is null)
            return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
            return;

        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                if (IsDebugEnabled(cmdlet))
                    cmdlet.WriteDebug($"[{_categoryName}] {message}");
                break;

            case LogLevel.Information:
                if (IsVerboseEnabled(cmdlet))
                    cmdlet.WriteVerbose($"[{_categoryName}] {message}");
                break;

            case LogLevel.Warning:
                cmdlet.WriteWarning($"[{_categoryName}] {message}");
                break;

            case LogLevel.Error:
            case LogLevel.Critical:
                var errorRecord = CreateErrorRecord(exception, message, eventId);
                cmdlet.WriteError(errorRecord);
                break;
        }
    }

    private static bool IsVerboseEnabled(PSCmdlet cmdlet)
    {
        return cmdlet.MyInvocation.BoundParameters.ContainsKey("Verbose");
    }

    private static bool IsDebugEnabled(PSCmdlet cmdlet)
    {
        return cmdlet.MyInvocation.BoundParameters.ContainsKey("Debug");
    }

    private ErrorRecord CreateErrorRecord(Exception? exception, string message, EventId eventId)
    {
        var exceptionToUse = exception ?? new Exception(message);
        var errorId = eventId.Id != 0 ? eventId.ToString() : "LoggingError";

        return new ErrorRecord(
            exceptionToUse,
            errorId,
            ErrorCategory.NotSpecified,
            null)
        {
            ErrorDetails = new ErrorDetails($"[{_categoryName}] {message}")
        };
    }

    private class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}

