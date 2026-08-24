using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class NewVideoEncodingSettingsCommandTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public NewVideoEncodingSettingsCommandTests()
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactoryMock.Object);
        services.AddSingleton(Mock.Of<IDebuggerService>());
        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void NewVideoEncodingSettings_WithCrfParameterSet_ReturnsConstantRateSettings()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-VideoEncodingSettings")
            .AddParameter("Codec", "libx264")
            .AddParameter("CRF", 22)
            .AddParameter("Preset", "medium")
            .AddParameter("CodecProfile", "high")
            .AddParameter("Tune", "film");

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var settings = Assert.IsType<ConstantRateVideoEncodingSettings>(Assert.Single(results).BaseObject);
        Assert.Equal("libx264", settings.Codec);
        Assert.Equal(22, settings.CRF);
        Assert.Equal("medium", settings.Preset);
        Assert.Equal("high", settings.CodecProfile);
        Assert.Equal("film", settings.Tune);
        Assert.Equal("yuv420p", settings.PixelFormat);
    }

    [Fact]
    public void NewVideoEncodingSettings_WithVbrParameterSet_ReturnsVariableRateSettings()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-VideoEncodingSettings")
            .AddParameter("Codec", "libx265")
            .AddParameter("Bitrate", 8000)
            .AddParameter("PixelFormat", "yuv420p10le");

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var settings = Assert.IsType<VariableRateVideoEncodingSettings>(Assert.Single(results).BaseObject);
        Assert.Equal("libx265", settings.Codec);
        Assert.Equal(8000, settings.Bitrate);
        Assert.Equal("yuv420p10le", settings.PixelFormat);
        Assert.False(settings.IsSinglePass);
    }

    private static PowerShell CreatePowerShell() =>
        PowerShellCmdletTestHost.Create<NewVideoEncodingSettingsCommand>("New-VideoEncodingSettings");
}
