using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using Dadstart.Labs.MediaForge.Services;

namespace Dadstart.Labs.MediaForge.Module;

/// <summary>
/// Adapts a live <see cref="PSCmdlet"/> to <see cref="ICmdletIO"/>.
/// </summary>
public sealed class PsCmdletIO(PSCmdlet cmdlet) : ICmdletIO
{
    public ICmdletPathContext Paths { get; } = new PsCmdletPathContext(cmdlet);

    public void WriteProgress(ProgressRecord record) => cmdlet.WriteProgress(record);

    public void WriteError(ErrorRecord error) => cmdlet.WriteError(error);

    public void WriteWarning(string message) => cmdlet.WriteWarning(message);

    public void WriteVerbose(string message) => cmdlet.WriteVerbose(message);

    public void UpdateProgressTitlePercent(int percentComplete)
    {
        var rawUi = TryGetRawUi();
        if (rawUi == null)
            return;

        try
        {
            rawUi.WindowTitle = MediaConversionHelper.FormatTitleWithProgressPercent(
                rawUi.WindowTitle,
                percentComplete);
        }
        catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
        {
            // Best effort; ignore hosts that do not support title updates.
        }
    }

    public void ClearProgressTitlePercent()
    {
        var rawUi = TryGetRawUi();
        if (rawUi == null)
            return;

        try
        {
            rawUi.WindowTitle = MediaConversionHelper.RemoveProgressPercentFromTitle(rawUi.WindowTitle);
        }
        catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
        {
            // Best effort; ignore hosts that do not support title updates.
        }
    }

    private PSHostRawUserInterface? TryGetRawUi()
    {
        try
        {
            return cmdlet.Host?.UI?.RawUI;
        }
        catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
        {
            return null;
        }
    }

    private sealed class PsCmdletPathContext(PSCmdlet cmdlet) : ICmdletPathContext
    {
        public string CurrentLocationPath => cmdlet.SessionState.Path.CurrentLocation.Path;

        public IList<string> GetResolvedProviderPaths(string path) =>
            cmdlet.GetResolvedProviderPathFromPSPath(path, out _);

        public string GetUnresolvedProviderPath(string path) =>
            cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path);
    }
}
