using System.Management.Automation;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Dadstart.Labs.MediaForge.DependencyInjection;
using Dadstart.Labs.MediaForge.Logging;
using System.ComponentModel;
using System.Diagnostics;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Base class for MediaForge PowerShell cmdlets that provides common functionality
/// for logging and PowerShell command context management.
/// </summary>
public abstract class MediaForgeCmdletBase : PSCmdlet
{
    private ILogger? _logger;
    private IPowerShellCommandContextAccessor? _contextAccessor;

    /// <summary>
    /// Logger instance for the derived cmdlet type.
    /// </summary>
    protected ILogger Logger
    {
        get
        {
            return _logger ??= ServiceProviderAccessor.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(GetType());
        }
    }

    /// <summary>
    /// PowerShell command context accessor for logging integration.
    /// </summary>
    protected IPowerShellCommandContextAccessor ContextAccessor
    {
        get
        {
            return _contextAccessor ??= ServiceProviderAccessor.ServiceProvider.GetRequiredService<IPowerShellCommandContextAccessor>();
        }
    }

    /// <summary>
    /// Sets up the PowerShell command context for logging before processing begins.
    /// </summary>
    protected sealed override void BeginProcessing()
    {
        ContextAccessor.SetCurrentContext(this);

        // Capture the synchronization context from the cmdlet's thread so we can marshal
        // Write calls back to this thread from async contexts
        var syncContext = SynchronizationContext.Current;
        ContextAccessor.SetSynchronizationContext(syncContext);

        Logger.LogDebug("Begin processing {CmdletName} command", GetType().Name);

        Begin();
    }

    protected sealed override void ProcessRecord() => Process();

    /// <summary>
    /// Cleans up the PowerShell command context after processing completes.
    /// </summary>
    protected sealed override void EndProcessing()
    {
        Logger.LogDebug("End processing {CmdletName} command", GetType().Name);

        End();

        ContextAccessor.SetCurrentContext(null);
        ContextAccessor.SetSynchronizationContext(null);
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
    /// Override this method to perform custom cleanup logic when processing emds.
    /// This method is called by EndProcessing after any necessary setup
    /// </summary>
    protected virtual void End()
    {
    }
}

