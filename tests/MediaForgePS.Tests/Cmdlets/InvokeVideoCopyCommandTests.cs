using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.SeriesProcessing;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class InvokeVideoCopyCommandTests : IDisposable
{
    private readonly Mock<ISeriesProcessingService> _seriesProcessingServiceMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<IDebuggerService> _debuggerServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;
    private readonly List<string> _tempDirectories = [];

    public InvokeVideoCopyCommandTests()
    {
        _loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
        _debuggerServiceMock.Setup(debugger => debugger.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
        services.AddSingleton(_seriesProcessingServiceMock.Object);
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();

        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void InvokeVideoCopy_WhenNoPathsProvided_WritesWarning()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-VideoCopy")
            .AddParameter("Title", "Show")
            .AddParameter("Season", 1)
            .AddParameter("Path", new[] { "  ", "\t" })
            .AddParameter("FilePatterns", "*.mkv")
            .AddParameter("Destination", CreateTempDirectory())
            .AddParameter("Episodes", CreateEpisodes())
            .AddParameter("Confirm", false);

        _ = ps.Invoke();
        var warnings = ps.Streams.Warning.ReadAll();

        Assert.Contains(warnings, warning => warning.Message.Contains("No input paths were provided", StringComparison.Ordinal));
        _seriesProcessingServiceMock.Verify(
            service => service.InvokeVideoCopy(It.IsAny<ICmdletIO>(), It.IsAny<VideoCopyRequest>()),
            Times.Never);
    }

    [Fact]
    public void InvokeVideoCopy_WhenSuccessful_ReturnsCopiedPaths()
    {
        var sourceDir = CreateTempDirectory();
        var destination = CreateTempDirectory();
        var copiedPath = Path.Combine(destination, "Show {tvdb 1} - s01e01.mkv");
        _seriesProcessingServiceMock
            .Setup(service => service.InvokeVideoCopy(It.IsAny<ICmdletIO>(), It.IsAny<VideoCopyRequest>()))
            .Returns([copiedPath]);

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-VideoCopy")
            .AddParameter("Title", "Show")
            .AddParameter("Season", 1)
            .AddParameter("Path", sourceDir)
            .AddParameter("FilePatterns", "*.mkv")
            .AddParameter("Destination", destination)
            .AddParameter("Episodes", CreateEpisodes())
            .AddParameter("Confirm", false);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Equal(copiedPath, Assert.Single(results).BaseObject);
        _seriesProcessingServiceMock.Verify(
            service => service.InvokeVideoCopy(
                It.IsAny<ICmdletIO>(),
                It.Is<VideoCopyRequest>(request =>
                    request.Title == "Show"
                    && request.Season == 1
                    && request.Destination == destination
                    && request.Paths.Single() == sourceDir)),
            Times.Once);
    }

    [Fact]
    public void InvokeVideoCopy_WhenWhatIfSpecified_DoesNotInvokeCopy()
    {
        var sourceDir = CreateTempDirectory();
        var destination = CreateTempDirectory();

        using var ps = CreatePowerShell();
        ps.AddCommand("Invoke-VideoCopy")
            .AddParameter("Title", "Show")
            .AddParameter("Season", 1)
            .AddParameter("Path", sourceDir)
            .AddParameter("FilePatterns", "*.mkv")
            .AddParameter("Destination", destination)
            .AddParameter("Episodes", CreateEpisodes())
            .AddParameter("WhatIf")
            .AddParameter("Confirm", false);

        _ = ps.Invoke();

        _seriesProcessingServiceMock.Verify(
            service => service.InvokeVideoCopy(It.IsAny<ICmdletIO>(), It.IsAny<VideoCopyRequest>()),
            Times.Never);
    }

    private static TvDbEpisodeInfo[] CreateEpisodes() =>
        [new TvDbEpisodeInfo("1", 1, "Pilot", 1)];

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_VideoCopy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private static PowerShell CreatePowerShell() =>
        PowerShellCmdletTestHost.Create<InvokeVideoCopyCommand>("Invoke-VideoCopy");
}
