using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Module;

namespace Dadstart.Labs.MediaForge.Tests.TestInfrastructure;

/// <summary>
/// Collecting <see cref="ICmdletIO"/> double for unit tests (no PowerShell runspace).
/// </summary>
public sealed class FakeCmdletIO : ICmdletIO
{
    public List<ProgressRecord> ProgressRecords { get; } = [];
    public List<ErrorRecord> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> VerboseMessages { get; } = [];
    public FakeCmdletPathContext Paths { get; } = new();

    ICmdletPathContext ICmdletIO.Paths => Paths;

    public void WriteProgress(ProgressRecord record) => ProgressRecords.Add(record);

    public void WriteError(ErrorRecord error) => Errors.Add(error);

    public void WriteWarning(string message) => Warnings.Add(message);

    public void WriteVerbose(string message) => VerboseMessages.Add(message);
}

/// <summary>
/// Stub path context that treats paths as literal filesystem paths.
/// </summary>
public sealed class FakeCmdletPathContext : ICmdletPathContext
{
    public string CurrentLocationPath { get; set; } = Directory.GetCurrentDirectory();

    public Func<string, IList<string>>? ResolveProviderPaths { get; set; }

    public Func<string, string>? ResolveUnresolvedProviderPath { get; set; }

    public IList<string> GetResolvedProviderPaths(string path) =>
        ResolveProviderPaths?.Invoke(path) ?? [Path.GetFullPath(path)];

    public string GetUnresolvedProviderPath(string path) =>
        ResolveUnresolvedProviderPath?.Invoke(path)
        ?? (Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(CurrentLocationPath, path)));
}
