using System.Management.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Dadstart.Labs.MediaForge.DependencyInjection;
using Dadstart.Labs.MediaForge.Logging;

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
    protected override void BeginProcessing()
    {
        ContextAccessor.SetCurrentContext(this);
        Logger.LogDebug("Begin processing {CmdletName} command", GetType().Name);
    }

    /// <summary>
    /// Cleans up the PowerShell command context after processing completes.
    /// </summary>
    protected override void EndProcessing()
    {
        Logger.LogDebug("End processing {CmdletName} command", GetType().Name);
        ContextAccessor.SetCurrentContext(null);
    }
}

