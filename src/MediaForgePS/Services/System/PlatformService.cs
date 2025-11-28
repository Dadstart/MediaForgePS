namespace Dadstart.Labs.MediaForge.Services.System;

public class PlatformService : IPlatformService
{
    public bool IsWindows()
    {
        return OperatingSystem.IsWindows();
    }
}
