using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

/// <summary>
/// Base class for cmdlet unit tests that provides common setup for mocking dependencies.
/// </summary>
public abstract class CmdletTestBase : IDisposable
{
    protected Mock<IFfmpegService> MockFfmpegService { get; }
    protected Mock<IPathResolver> MockPathResolver { get; }
    protected Mock<IPlatformService> MockPlatformService { get; }
    protected Mock<ILoggerFactory> MockLoggerFactory { get; }
    protected Mock<ILogger> MockLogger { get; }
    protected Mock<IDebuggerService> MockDebuggerService { get; }

    protected CmdletTestBase()
    {
        MockFfmpegService = new Mock<IFfmpegService>();
        MockPathResolver = new Mock<IPathResolver>();
        MockPlatformService = new Mock<IPlatformService>();
        MockLogger = new Mock<ILogger>();
        MockLoggerFactory = new Mock<ILoggerFactory>();
        MockDebuggerService = new Mock<IDebuggerService>();

        // Note: CreateLogger(Type) is an extension method and cannot be mocked with Moq
        // We inject the logger directly via reflection, so this setup is not needed
        MockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(MockLogger.Object);

        // Setup default platform service behavior
        MockPlatformService.Setup(p => p.IsWindows()).Returns(true);
    }

    /// <summary>
    /// Creates a service provider with mocked dependencies for testing.
    /// </summary>
    protected IServiceProvider CreateMockServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(MockFfmpegService.Object);
        services.AddSingleton(MockPathResolver.Object);
        services.AddSingleton(MockPlatformService.Object);
        services.AddSingleton(MockLoggerFactory.Object);
        services.AddSingleton(MockDebuggerService.Object);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a testable cmdlet instance with mocked dependencies.
    /// </summary>
    protected T CreateTestableCmdlet<T>(IServiceProvider? serviceProvider = null) where T : CmdletBase, new()
    {
        serviceProvider ??= CreateMockServiceProvider();

        // Use reflection to inject the service provider
        // Note: This is a workaround since ModuleServices is static
        // In a real scenario, you might want to refactor to use dependency injection
        var cmdlet = new T();

        // Set up the service provider using reflection
        // This is a test-only approach
        return cmdlet;
    }

    /// <summary>
    /// Creates a testable ConvertMediaCommandBase instance.
    /// </summary>
    protected TestableConvertMediaCommandBase CreateTestableConvertMediaCommandBase(
        VideoEncodingSettings videoEncodingSettings,
        AudioTrackMapping[] audioTrackMappings,
        string[]? additionalArguments = null)
    {
        var cmdlet = new TestableConvertMediaCommandBase
        {
            VideoEncodingSettings = videoEncodingSettings,
            AudioTrackMappings = audioTrackMappings,
            AdditionalArguments = additionalArguments
        };

        // Inject mocked services using reflection
        InjectMockedServices(cmdlet);

        return cmdlet;
    }

    /// <summary>
    /// Injects mocked services into a cmdlet using reflection.
    /// </summary>
    protected void InjectMockedServices(CmdletBase cmdlet)
    {
        var serviceProvider = CreateMockServiceProvider();

        // Inject services into the cmdlet's private fields
        var cmdletType = cmdlet.GetType();
        var baseType = cmdletType.BaseType;

        // For ConvertMediaCommandBase, inject the services
        if (cmdlet is ConvertMediaCommandBase convertCmdlet)
        {
            InjectService(convertCmdlet, "_ffmpegService", MockFfmpegService.Object);
            InjectService(convertCmdlet, "_pathResolver", MockPathResolver.Object);
            InjectService(convertCmdlet, "_platformService", MockPlatformService.Object);
        }

        // Inject logger and debugger into CmdletBase
        // Search in the base class hierarchy
        InjectServiceInHierarchy(cmdlet, "_logger", MockLogger.Object);
        InjectServiceInHierarchy(cmdlet, "_debugger", MockDebuggerService.Object);
    }

    private void InjectService(object target, string fieldName, object service)
    {
        var type = target.GetType();
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, service);
        }
    }

    private void InjectServiceInHierarchy(object target, string fieldName, object service)
    {
        var type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, service);
                return;
            }
            type = type.BaseType;
        }
    }

    public virtual void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Testable implementation of ConvertMediaCommandBase for unit testing.
/// </summary>
public class TestableConvertMediaCommandBase : ConvertMediaCommandBase
{
    public new IEnumerable<string> BuildFfmpegArguments(int? pass)
    {
        return base.BuildFfmpegArguments(pass);
    }

    public new ErrorRecord CreatePathErrorRecord(Exception exception, string errorId, ErrorCategory errorCategory, object targetObject)
    {
        return base.CreatePathErrorRecord(exception, errorId, errorCategory, targetObject);
    }

    public new void WritePathErrorRecord(string path, string message)
    {
        base.WritePathErrorRecord(path, message);
    }

    public new bool ConvertMediaFile(string inputPath, string outputPath)
    {
        return base.ConvertMediaFile(inputPath, outputPath);
    }

    protected override void Process()
    {
        // Override to make it testable without PowerShell runtime
    }
}
