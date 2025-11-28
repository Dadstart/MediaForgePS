using System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Module;

/// <summary>
/// ILoggerProvider that creates PowerShellLogger instances.
/// </summary>
public class PowerShellLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new PowerShellLogger(categoryName);

    public void Dispose()
    {
        // Nothing to dispose
    }
}
