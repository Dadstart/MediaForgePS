using System.Management.Automation;

namespace Dadstart.Labs.MediaForge.Module;

/// <summary>
/// Applies progress percentages to the terminal title while progress is displayed.
/// </summary>
internal interface ITerminalProgressTitle
{
    void ApplyProgressToTerminalTitle(ProgressRecord record);
}
