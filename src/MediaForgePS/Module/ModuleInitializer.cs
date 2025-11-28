using Dadstart.Labs.MediaForge.Services;

namespace Dadstart.Labs.MediaForge.Module;

/// <summary>
/// Initializes and manages the lifecycle of module services.
/// Handles initialization of the dependency injection container and cleanup on module unload.
/// </summary>
public static class ModuleInitializer
{
    /// <summary>
    /// Initializes the module services and sets up cleanup handlers for process exit and unhandled exceptions.
    /// </summary>
    public static void Initialize()
    {
        // Ensure the DI container is created when the assembly loads
        ModuleServices.EnsureInitialized();

        // Dispose the module services when the process exits (PowerShell session end)
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            try
            {
                ModuleServices.Dispose();
            }
            catch
            {
                // Exceptions during process exit cleanup are ignored to prevent cascading failures
            }
        };

        // handle unhandled exceptions to attempt cleanup
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                ModuleServices.Dispose();
            }
            catch
            {
                // Exceptions during unhandled exception cleanup are ignored to prevent cascading failures
            }
        };
    }

    /// <summary>
    /// Cleans up module services. Called when the module is removed from the PowerShell session.
    /// </summary>
    public static void Cleanup()
    {
        try
        {
            ModuleServices.Dispose();
        }
        catch
        {
            // Exceptions during module cleanup are ignored to prevent cascading failures
        }
    }
}
