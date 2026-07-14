using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Module;

public sealed class PowerShellLoggerTests
{
    [Theory]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void Log_WarningErrorAndCritical_WriteWarningNotError(LogLevel level)
    {
        lock (LoggerProbeCmdlet.SyncRoot)
        {
            LoggerProbeCmdlet.Reset(level, includeException: false);
            using var ps = PowerShellCmdletTestHost.Create<LoggerProbeCmdlet>("Invoke-LoggerProbe");
            ps.AddCommand("Invoke-LoggerProbe");

            _ = ps.Invoke();

            Assert.Empty(ps.Streams.Error);
            Assert.Contains(ps.Streams.Warning, w => w.Message.Contains("probe-message", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Log_ErrorWithException_IncludesExceptionInWarning()
    {
        lock (LoggerProbeCmdlet.SyncRoot)
        {
            LoggerProbeCmdlet.Reset(LogLevel.Error, includeException: true);
            using var ps = PowerShellCmdletTestHost.Create<LoggerProbeCmdlet>("Invoke-LoggerProbe");
            ps.AddCommand("Invoke-LoggerProbe");

            _ = ps.Invoke();

            Assert.Empty(ps.Streams.Error);
            var warning = Assert.Single(ps.Streams.Warning);
            Assert.Contains("probe-message", warning.Message, StringComparison.Ordinal);
            Assert.Contains("probe-exception", warning.Message, StringComparison.Ordinal);
        }
    }

    [Cmdlet(VerbsLifecycle.Invoke, "LoggerProbe")]
    private sealed class LoggerProbeCmdlet : PSCmdlet
    {
        public static readonly object SyncRoot = new();

        private static LogLevel _level;
        private static bool _includeException;

        public static void Reset(LogLevel level, bool includeException)
        {
            _level = level;
            _includeException = includeException;
        }

        protected override void ProcessRecord()
        {
            CmdletContext.Current = this;
            try
            {
                var logger = new PowerShellLogger("Probe");
                if (_includeException)
                    logger.Log(_level, new EventId(1), "probe-message", new InvalidOperationException("probe-exception"), static (s, _) => s);
                else
                    logger.Log(_level, new EventId(1), "probe-message", null, static (s, _) => s);
            }
            finally
            {
                CmdletContext.Current = null;
            }
        }
    }
}
