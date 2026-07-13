using System.Collections.Generic;
using System.Management.Automation;

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

    private sealed class PsCmdletPathContext(PSCmdlet cmdlet) : ICmdletPathContext
    {
        public string CurrentLocationPath => cmdlet.SessionState.Path.CurrentLocation.Path;

        public IList<string> GetResolvedProviderPaths(string path) =>
            cmdlet.GetResolvedProviderPathFromPSPath(path, out _);

        public string GetUnresolvedProviderPath(string path) =>
            cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path);
    }
}
