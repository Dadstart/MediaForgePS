using Dadstart.Labs.MediaForge.Services;

namespace Dadstart.Labs.MediaForge.Module;

internal static class ModuleInitializer
{
    public static void Initialize()
    {
        // Ensure the DI container is created when the assembly loads
        ModuleServices.EnsureInitialized();

        // Dispose the module services when the process exits (PowerShell session end)
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            try { ModuleServices.Dispose(); } catch { }
        };

        // handle unhandled exceptions to attempt cleanup
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try { ModuleServices.Dispose(); } catch { }
        };
    }
}