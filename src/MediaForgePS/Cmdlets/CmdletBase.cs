using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Base class for MediaForge PowerShell cmdlets that supports async and
/// provides common functionality for logging and other.
/// </summary>
public abstract class CmdletBase : PSCmdlet
{
    /// <summary>
    /// Activity ID for the main operation in progress records (e.g. batch or top-level task).
    /// </summary>
    protected const int MainActivityId = 0;

    /// <summary>
    /// Activity ID for the current item in progress records (e.g. current file or stream).
    /// </summary>
    protected const int CurrentItemActivityId = 1;

    private IDebuggerService? _debugger;
    private ILogger? _logger;

    /// <summary>
    /// Logger instance for the derived cmdlet type.
    /// </summary>
    protected ILogger Logger => _logger ??= ModuleServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(GetType());

    protected IDebuggerService Debugger => _debugger ??= ModuleServices.GetRequiredService<IDebuggerService>();

    public string CmdletName => GetType().Name;

    protected CmdletBase()
    {
        ModuleServices.EnsureInitialized();
        CmdletContext.Current = this;

    }

    /// <summary>
    /// Sets up the PowerShell command context for logging before processing begins.
    /// </summary>
    protected sealed override void BeginProcessing()
    {
        CmdletContext.Current = this;
        Debugger.BreakIfDebugging(Debugger.PowerShellBreakOnBeginProcessing);

        Logger.LogDebug("Begin processing {CmdletName} command", CmdletName);
        Begin();
    }

    /// <summary>
    /// Processes each record in the pipeline.
    /// Handles common behavior and then calls child Process to do the actual record process
    /// </summary>
    protected sealed override void ProcessRecord()
    {
        Debugger.BreakIfDebugging(Debugger.PowerShellBreakOnProcessRecord);

        Logger.LogDebug("Processing {CmdletName} command", CmdletName);
        Process();
    }

    /// <summary>
    /// Cleans up the PowerShell command context after processing completes.
    /// </summary>
    protected sealed override void EndProcessing()
    {
        Debugger.BreakIfDebugging(Debugger.PowerShellBreakOnEndProcessing);

        Logger.LogDebug("End processing {CmdletName} command", CmdletName);
        End();

        CmdletContext.Current = null;
    }

    /// <summary>
    /// Override this method to perform custom initialization logic when processing begins.
    /// This method is called by BeginProcessing after any necessary setup
    /// </summary>
    protected virtual void Begin()
    {
    }

    /// <summary>
    /// Override this method with processing logic.
    /// This method is called by ProcessRecord after any necessary setup
    /// </summary>
    protected virtual void Process()
    {
    }

    /// <summary>
    /// Override this method to perform custom cleanup logic when processing ends.
    /// This method is called by EndProcessing after any necessary setup
    /// </summary>
    protected virtual void End()
    {
    }

    /// <summary>
    /// Writes a message to the host (information stream) with optional foreground color.
    /// Use for user-facing status and milestone messages that should appear in the console.
    /// </summary>
    /// <param name="message">Message to display.</param>
    /// <param name="foregroundColor">Optional console color for the message.</param>
    protected void WriteHostMessage(string message, ConsoleColor? foregroundColor = null)
    {
        var hostMsg = new HostInformationMessage { Message = message };
        if (foregroundColor.HasValue)
            hostMsg.ForegroundColor = foregroundColor.Value;
        WriteInformation(new InformationRecord(hostMsg, "PSHOST"));
    }
}
