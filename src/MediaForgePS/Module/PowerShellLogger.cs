using System;
using Microsoft.Extensions.Logging;
using System.Management.Automation;

namespace Dadstart.Labs.MediaForge.Module
{
    /// <summary>
    /// ILogger implementation that forwards log messages into the current PSCmdlet streams.
    /// If no cmdlet is present in CmdletContext.Current, messages are ignored.
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

        public void Log<TState>(LogLevel level, EventId id,
            TState state, Exception? exc, Func<TState, Exception?, string> formatter)
        {
            try
            {
                LogCore(level, id, state, exc, formatter);
            }
            catch
            {
                // catch logging exceptions
            }
        }

        private void LogCore<TState>(LogLevel level, EventId id,
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
                case LogLevel.Debug:
                    // Use WriteVerbose for informational messages so they respect -Verbose
                    cmdlet.WriteVerbose(output);
                    break;
                case LogLevel.Information:
                    var infoRecord = new InformationRecord(output, _category);
                    cmdlet.WriteInformation(infoRecord);
                    break;
                case LogLevel.Warning:
                    cmdlet.WriteWarning(output);
                    break;

                case LogLevel.Error:
                case LogLevel.Critical:
                    // Create an ErrorRecord for WriteError
                    var ex = exc ?? new Exception(output);
                    var record = new ErrorRecord(ex, id.Id.ToString(), ErrorCategory.NotSpecified, null);
                    cmdlet.WriteError(record);
                    break;

                default:
                    cmdlet.WriteVerbose(output);
                    break;
            }
        }

        private class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}