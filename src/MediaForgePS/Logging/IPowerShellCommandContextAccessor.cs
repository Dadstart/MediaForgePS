using System.Management.Automation;
using System.Threading;

namespace Dadstart.Labs.MediaForge.Logging;

/// <summary>
/// Provides access to the current PowerShell command context (PSCmdlet) for logging purposes.
/// </summary>
public interface IPowerShellCommandContextAccessor
{
    /// <summary>
    /// Gets the current PowerShell command context, if available.
    /// </summary>
    PSCmdlet? GetCurrentContext();

    /// <summary>
    /// Sets the current PowerShell command context.
    /// </summary>
    void SetCurrentContext(PSCmdlet? cmdlet);

    /// <summary>
    /// Gets the synchronization context for the cmdlet's thread, if available.
    /// </summary>
    SynchronizationContext? GetSynchronizationContext();

    /// <summary>
    /// Sets the synchronization context for the cmdlet's thread.
    /// </summary>
    void SetSynchronizationContext(SynchronizationContext? context);
}

