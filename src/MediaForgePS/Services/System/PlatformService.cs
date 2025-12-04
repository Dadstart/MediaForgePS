namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Service for platform-specific operations.
/// </summary>
public class PlatformService : IPlatformService
{
    /// <inheritdoc />
    public bool IsWindows()
    {
        return OperatingSystem.IsWindows();
    }

    /// <inheritdoc />
    public string QuoteArgument(string argument)
    {
        return ProcessArgumentExtensions.QuoteArgument(argument, this);
    }
}
