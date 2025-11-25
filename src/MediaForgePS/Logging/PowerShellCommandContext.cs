using System.Management.Automation;
using System.Threading;

namespace Dadstart.Labs.MediaForge.Logging;

/// <summary>
/// Thread-safe implementation of PowerShell command context accessor using AsyncLocal for thread isolation.
/// </summary>
public class PowerShellCommandContext : IPowerShellCommandContextAccessor
{
    private static readonly AsyncLocal<PSCmdlet?> _currentContext = new();

    /// <inheritdoc />
    public PSCmdlet? GetCurrentContext()
    {
        return _currentContext.Value;
    }

    /// <inheritdoc />
    public void SetCurrentContext(PSCmdlet? cmdlet)
    {
        _currentContext.Value = cmdlet;
    }
}

