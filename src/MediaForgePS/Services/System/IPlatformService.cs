namespace Dadstart.Labs.MediaForge.Services.System;

public interface IPlatformService
{
    bool IsWindows();

    /// <summary>
    /// Quotes a single argument according to platform-specific rules.
    /// </summary>
    /// <param name="argument">The argument to quote.</param>
    /// <returns>A properly quoted argument string suitable for ProcessStartInfo.Arguments.</returns>
    string QuoteArgument(string argument);
}
