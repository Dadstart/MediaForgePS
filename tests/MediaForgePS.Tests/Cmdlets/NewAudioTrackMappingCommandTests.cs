using System;
using System.Linq;
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

public sealed class NewAudioTrackMappingCommandTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public NewAudioTrackMappingCommandTests()
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
    public void NewAudioTrackMapping_WithCopyParameterSet_ReturnsCopyMapping()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-AudioTrackMapping")
            .AddParameter("Copy")
            .AddParameter("SourceStream", 0)
            .AddParameter("SourceIndex", 1)
            .AddParameter("DestinationIndex", 0)
            .AddParameter("Title", "English");

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var mapping = Assert.IsType<CopyAudioTrackMapping>(Assert.Single(results).BaseObject);
        Assert.Equal("English", mapping.Title);
        Assert.Equal(0, mapping.SourceStream);
        Assert.Equal(1, mapping.SourceIndex);
        Assert.Equal(0, mapping.DestinationIndex);
    }

    [Fact]
    public void NewAudioTrackMapping_WithEncodeParameterSet_ReturnsEncodeMapping()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-AudioTrackMapping")
            .AddParameter("Encode")
            .AddParameter("SourceStream", 0)
            .AddParameter("SourceIndex", 2)
            .AddParameter("DestinationIndex", 1)
            .AddParameter("Codec", "aac")
            .AddParameter("Channels", 2)
            .AddParameter("Bitrate", 192);

        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var mapping = Assert.IsType<EncodeAudioTrackMapping>(Assert.Single(results).BaseObject);
        Assert.Equal(2, mapping.SourceIndex);
        Assert.Equal(1, mapping.DestinationIndex);
        Assert.Equal("aac", mapping.DestinationCodec);
        Assert.Equal(2, mapping.DestinationChannels);
        Assert.Equal(192, mapping.DestinationBitrate);
    }

    private static PowerShell CreatePowerShell() =>
        PowerShellCmdletTestHost.Create<NewAudioTrackMappingCommand>("New-AudioTrackMapping");
}
