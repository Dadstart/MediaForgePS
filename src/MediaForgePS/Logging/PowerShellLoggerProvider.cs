using System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Logging;

/// <summary>
/// Logger provider that creates PowerShell-aware loggers.
/// </summary>
public class PowerShellLoggerProvider : ILoggerProvider
{
    private readonly IPowerShellCommandContextAccessor _contextAccessor;
    private readonly Func<string, LogLevel, bool>? _filter;

    public PowerShellLoggerProvider(
        IPowerShellCommandContextAccessor contextAccessor,
        Func<string, LogLevel, bool>? filter = null)
    {
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _filter = filter;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new PowerShellLogger(categoryName, _contextAccessor, _filter);
    }

    public void Dispose()
    {
    }
}

