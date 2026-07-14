namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Service for platform-specific operations.
/// </summary>
public class PlatformService : IPlatformService
{
    /// <inheritdoc />
    public bool IsWindows() => OperatingSystem.IsWindows();
}
