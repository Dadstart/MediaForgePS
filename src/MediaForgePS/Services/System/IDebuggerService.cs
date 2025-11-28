namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Service for managing debugging state and breakpoint behavior.
/// </summary>
public interface IDebuggerService
{
    /// <summary>
    /// Indicates whether debugging is currently active, either through a forced state or when a debugger is already attached.
    /// </summary>
    bool IsDebugging { get; }

    /// <summary>
    /// Controls whether to break execution at the BeginProcessing stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    bool PowerShellBreakOnBeginProcessing { get; }

    /// <summary>
    /// Controls whether to break execution at the ProcessRecord stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    bool PowerShellBreakOnProcessRecord { get; }

    /// <summary>
    /// Controls whether to break execution at the EndProcessing stage of PowerShell cmdlets when debugging is active.
    /// </summary>
    bool PowerShellBreakOnEndProcessing { get; }

    /// <summary>
    /// Breaks in to the debugger if debugging flag is true
    /// </summary>
    void BreakIfDebugging(bool flag);
}
