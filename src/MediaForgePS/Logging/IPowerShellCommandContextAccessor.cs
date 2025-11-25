using System.Management.Automation;

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
}

