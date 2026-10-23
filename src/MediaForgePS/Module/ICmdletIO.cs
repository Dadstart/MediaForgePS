using System.Collections.Generic;
using System.Management.Automation;

namespace Dadstart.Labs.MediaForge.Module;

/// <summary>
/// Writes ProgressRecord values to the host progress stream.
/// </summary>
public interface ICmdletProgress
{
    void WriteProgress(ProgressRecord record);

    /// <summary>
    /// Appends or updates the percent complete on the terminal window title.
    /// </summary>
    /// <param name="percentComplete">Percentage complete (0-100).</param>
    void UpdateProgressTitlePercent(int percentComplete);

    /// <summary>
    /// Removes any appended progress percent from the terminal window title.
    /// </summary>
    void ClearProgressTitlePercent();
}

/// <summary>
/// Writes error records to the host error stream.
/// </summary>
public interface ICmdletErrorSink
{
    void WriteError(ErrorRecord error);
}

/// <summary>
/// Writes warning messages to the host warning stream.
/// </summary>
public interface ICmdletWarningSink
{
    void WriteWarning(string message);
}

/// <summary>
/// Writes verbose messages to the host verbose stream.
/// </summary>
public interface ICmdletVerboseSink
{
    void WriteVerbose(string message);
}

/// <summary>
/// PowerShell location and provider path resolution without a live PSCmdlet dependency in services.
/// </summary>
public interface ICmdletPathContext
{
    /// <summary>
    /// Absolute path of the session's current location.
    /// </summary>
    string CurrentLocationPath { get; }

    /// <summary>
    /// Resolves a provider path that must exist.
    /// </summary>
    IList<string> GetResolvedProviderPaths(string path);

    /// <summary>
    /// Resolves a provider path without requiring the target to exist.
    /// </summary>
    string GetUnresolvedProviderPath(string path);
}

/// <summary>
/// Host I/O facade used by services for progress, streams, and path resolution.
/// </summary>
public interface ICmdletIO : ICmdletProgress, ICmdletErrorSink, ICmdletWarningSink, ICmdletVerboseSink
{
    /// <summary>
    /// Path resolution against the current PowerShell session.
    /// </summary>
    ICmdletPathContext Paths { get; }
}
