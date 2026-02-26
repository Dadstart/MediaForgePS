namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Shared activity IDs for progress records used across cmdlets and services.
/// </summary>
public static class ProgressActivityIds
{
    /// <summary>Activity ID for the main operation (e.g. batch or top-level task).</summary>
    public const int Main = 0;

    /// <summary>Activity ID for the current item (e.g. current file or stream).</summary>
    public const int CurrentItem = 1;
}
