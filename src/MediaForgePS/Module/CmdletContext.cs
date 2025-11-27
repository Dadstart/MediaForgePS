using System.Management.Automation;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Dadstart.Labs.MediaForge.Module;

/// <summary>
/// Holds the current executing PSCmdlet in an AsyncLocal so loggers can forward to the correct cmdlet streams.
/// </summary>
public static class CmdletContext
{
    private static readonly AsyncLocal<PSCmdlet?> _current = new();
    public static PSCmdlet? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}