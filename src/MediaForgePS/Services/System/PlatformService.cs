namespace Dadstart.Labs.MediaForge.Services.System;

public class PlatformService : IPlatformService
{
    public bool IsWindows()
    {
        return OperatingSystem.IsWindows();
    }

    public string QuoteArgument(string argument)
    {
        return ProcessArgumentExtensions.QuoteArgument(argument, this);
    }
}
