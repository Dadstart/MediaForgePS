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

    private bool _forceDebugging;

    /// <summary>
    /// Indicates whether debugging is currently active, either through a forced state or when a debugger is already attached.
    /// </summary>
    public bool IsDebugging
    {
        get { return _forceDebugging || Debugger.IsAttached; }
        set { _forceDebugging = value; }
    }

    /// <summary>
    /// Controls whether to break execution at the BeginProcessing stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    public bool PowerShellBreakOnBeginProcessing
    {
        get => IsDebugging && _powerShellBreakOnBeginProcessing;
        set => _powerShellBreakOnBeginProcessing = value;
    }

    /// <summary>
    /// Controls whether to break execution at the ProcessRecord stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    public bool PowerShellBreakOnProcessRecord
    {
        get => IsDebugging && _powerShellBreakOnProcessRecord;
        set => _powerShellBreakOnProcessRecord = value;
    }

    /// <summary>
    /// Controls whether to break execution at the EndProcessing stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    public bool PowerShellBreakOnEndProcessing
    {
        get => IsDebugging && _powerShellBreakOnEndProcessing;
        set => _powerShellBreakOnEndProcessing = value;
    }

    public void BreakIfDebugging(bool flag)
    {
        if (flag)
            Debugger.Break();
    }
}