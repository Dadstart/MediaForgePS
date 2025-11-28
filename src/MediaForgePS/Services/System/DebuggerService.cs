using System.Diagnostics;

namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Service implementation for managing debugging state and breakpoint behavior.
/// </summary>
public class DebuggerService : IDebuggerService
{
    private bool _powerShellBreakOnBeginProcessing = false;
    private bool _powerShellBreakOnProcessRecord = false;
    private bool _powerShellBreakOnEndProcessing = false;

    public static bool ForceDebugging { get; set; } = false;

    public static bool BreakAll { get; set; } = false;

    /// <summary>
    /// Indicates whether debugging is currently active, either through a forced state or when a debugger is already attached.
    /// </summary>
    public bool IsDebugging => ForceDebugging || Debugger.IsAttached;

    private bool ShouldBreak(bool setting)
    {
        return IsDebugging && (BreakAll || setting);
    }

    /// <summary>
    /// Controls whether to break execution at the BeginProcessing stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    public bool PowerShellBreakOnBeginProcessing
    {
        get => ShouldBreak(_powerShellBreakOnBeginProcessing);
        set => _powerShellBreakOnBeginProcessing = value;
    }

    /// <summary>
    /// Controls whether to break execution at the ProcessRecord stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    public bool PowerShellBreakOnProcessRecord
    {
        get => ShouldBreak(_powerShellBreakOnProcessRecord);
        set => _powerShellBreakOnProcessRecord = value;
    }

    /// <summary>
    /// Controls whether to break execution at the EndProcessing stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    public bool PowerShellBreakOnEndProcessing
    {
        get => ShouldBreak(_powerShellBreakOnEndProcessing);
        set => _powerShellBreakOnEndProcessing = value;
    }

    public void BreakIfDebugging(bool flag)
    {
        if (flag)
            Debugger.Break();
    }
}
