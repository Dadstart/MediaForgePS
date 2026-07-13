using System;
using System.IO;
using System.Management.Automation;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Base class for long-running cmdlets that report progress.
/// Plays a system beep when the cmdlet finishes if <c>-Alert</c> is specified.
/// </summary>
public abstract class ProgressCmdletBase : CmdletBase
{
    /// <summary>
    /// Play a system beep when the cmdlet finishes.
    /// </summary>
    [Parameter(HelpMessage = "Play a system beep when the cmdlet finishes.")]
    public SwitchParameter Alert { get; set; }

    /// <inheritdoc />
    protected override void TryAlertOnCompletion()
    {
        if (!Alert)
            return;

        PlayCompletionAlert();
    }

    /// <summary>
    /// Plays the completion alert sound. Overridable for tests.
    /// </summary>
    protected virtual void PlayCompletionAlert()
    {
        try
        {
            Console.Beep();
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or InvalidOperationException)
        {
            Logger.LogDebug(ex, "Completion alert beep is unavailable for {CmdletName}", CmdletName);
        }
    }
}
