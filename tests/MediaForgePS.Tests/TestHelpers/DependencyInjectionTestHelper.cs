using Dadstart.Labs.MediaForge.DependencyInjection;

namespace Dadstart.Labs.MediaForge.Tests.TestHelpers;

public static class DependencyInjectionTestHelper
{
    public static void InitializeServiceProvider()
    {
        ModuleInitializer.Initialize();
    }

    public static void CleanupServiceProvider()
    {
        ModuleInitializer.Cleanup();
    }
}

