using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Providers;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Providers;

public class MediaCmdletProviderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _mediaPath;
    private readonly Mock<IMediaReaderService> _mediaReaderServiceMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public MediaCmdletProviderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "mediaforge-provider-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _mediaPath = Path.Combine(_tempRoot, "sample.mkv");
        File.WriteAllBytes(_mediaPath, [0]);

        _mediaReaderServiceMock = new Mock<IMediaReaderService>();
        _mediaReaderServiceMock
            .Setup(m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => CreateMediaFile(path));

        var services = new ServiceCollection();
        services.AddSingleton(_mediaReaderServiceMock.Object);
        services.AddSingleton(Mock.Of<ILoggerFactory>());
        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public void Provider_IsRegistered_AndListsMediaChildren()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-PSDrive")
            .AddParameter("Name", "mf")
            .AddParameter("PSProvider", "Media")
            .AddParameter("Root", _tempRoot);
        ps.Invoke();
        Assert.Empty(ps.Streams.Error);
        ps.Commands.Clear();

        ps.AddCommand("Get-ChildItem").AddParameter("Path", "mf:");
        var children = ps.Invoke();
        Assert.Empty(ps.Streams.Error);

        var mediaFile = children.Select(c => c.BaseObject).OfType<MediaFile>().Single();
        Assert.Equal(_mediaPath, mediaFile.Path);

        ps.Commands.Clear();
        ps.AddCommand("Get-ChildItem").AddParameter("Path", @"mf:\sample.mkv");
        var virtualChildren = ps.Invoke().Select(c => c.BaseObject).ToArray();
        Assert.Empty(ps.Streams.Error);
        Assert.Contains(virtualChildren, o => o is MediaFormat);
        Assert.Contains(virtualChildren, o => o is MediaContainerItem { Name: "chapters" });
        Assert.Contains(virtualChildren, o => o is MediaContainerItem { Name: "streams" });
    }

    [Fact]
    public void Provider_GetItem_StreamByTypeRelativeIndex()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-PSDrive")
            .AddParameter("Name", "mf")
            .AddParameter("PSProvider", "Media")
            .AddParameter("Root", _mediaPath);
        ps.Invoke();
        Assert.Empty(ps.Streams.Error);
        ps.Commands.Clear();

        ps.AddCommand("Get-Item").AddParameter("Path", @"mf:\streams\audio\0");
        var results = ps.Invoke();
        Assert.Empty(ps.Streams.Error);

        var stream = Assert.IsType<MediaStream>(Assert.Single(results).BaseObject);
        Assert.Equal("audio", stream.Type);
        Assert.Equal(1, stream.Index);
        Assert.Equal("aac", stream.Codec);
    }

    [Fact]
    public void Provider_GetItem_StreamByAbsoluteIndex()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-PSDrive")
            .AddParameter("Name", "mf")
            .AddParameter("PSProvider", "Media")
            .AddParameter("Root", _mediaPath);
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("Get-Item").AddParameter("Path", @"mf:\streams\all\2");
        var results = ps.Invoke();
        Assert.Empty(ps.Streams.Error);

        var stream = Assert.IsType<MediaStream>(Assert.Single(results).BaseObject);
        Assert.Equal("subtitle", stream.Type);
        Assert.Equal(2, stream.Index);
    }

    [Fact]
    public void Provider_GetChildItem_Chapters()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-PSDrive")
            .AddParameter("Name", "mf")
            .AddParameter("PSProvider", "Media")
            .AddParameter("Root", _mediaPath);
        ps.Invoke();
        ps.Commands.Clear();

        ps.AddCommand("Get-ChildItem").AddParameter("Path", @"mf:\chapters");
        var results = ps.Invoke().Select(r => r.BaseObject).OfType<MediaChapter>().ToArray();
        Assert.Empty(ps.Streams.Error);
        Assert.Equal(2, results.Length);
        Assert.Equal("Intro", results[0].Title);
        Assert.Equal("Main", results[1].Title);
    }

    [Fact]
    public void Provider_GetItem_StreamPathWithParentDots_Resolves()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-PSDrive")
            .AddParameter("Name", "mf")
            .AddParameter("PSProvider", "Media")
            .AddParameter("Root", _mediaPath);
        ps.Invoke();
        Assert.Empty(ps.Streams.Error);
        ps.Commands.Clear();

        // Unix PowerShell often rewrites file-rooted drive paths as mf:/../file.mkv/streams/...
        var dottedPath = "mf:" + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar
            + Path.GetFileName(_mediaPath) + Path.DirectorySeparatorChar
            + "streams" + Path.DirectorySeparatorChar + "audio" + Path.DirectorySeparatorChar + "0";
        ps.AddCommand("Get-Item").AddParameter("Path", dottedPath);
        var results = ps.Invoke();
        Assert.Empty(ps.Streams.Error);

        var stream = Assert.IsType<MediaStream>(Assert.Single(results).BaseObject);
        Assert.Equal("audio", stream.Type);
        Assert.Equal(1, stream.Index);
    }

    [Fact]
    public void Provider_TestPath_DoesNotProbeMediaOrValidateIndexBounds()
    {
        using var ps = CreatePowerShell();
        ps.AddCommand("New-PSDrive")
            .AddParameter("Name", "mf")
            .AddParameter("PSProvider", "Media")
            .AddParameter("Root", _mediaPath);
        ps.Invoke();
        Assert.Empty(ps.Streams.Error);
        ps.Commands.Clear();

        // Out-of-range indexes still "exist" for Test-Path: existence is path-shape + file presence only.
        ps.AddCommand("Test-Path").AddParameter("Path", @"mf:\chapters\99");
        var chapterExists = Assert.IsType<bool>(Assert.Single(ps.Invoke()).BaseObject);
        Assert.True(chapterExists);
        Assert.Empty(ps.Streams.Error);
        ps.Commands.Clear();

        ps.AddCommand("Test-Path").AddParameter("Path", @"mf:\streams\audio\99");
        var streamExists = Assert.IsType<bool>(Assert.Single(ps.Invoke()).BaseObject);
        Assert.True(streamExists);
        Assert.Empty(ps.Streams.Error);

        _mediaReaderServiceMock.Verify(
            m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Provider_TestPath_MissingMediaFile_ReturnsFalseWithoutError()
    {
        var missingMedia = Path.Combine(_tempRoot, "gone.mkv");
        File.WriteAllBytes(missingMedia, [0]);

        using var ps = CreatePowerShell();
        ps.AddCommand("New-PSDrive")
            .AddParameter("Name", "mf")
            .AddParameter("PSProvider", "Media")
            .AddParameter("Root", missingMedia);
        ps.Invoke();
        Assert.Empty(ps.Streams.Error);
        ps.Commands.Clear();

        File.Delete(missingMedia);

        ps.AddCommand("Test-Path").AddParameter("Path", @"mf:\streams\video\0");
        var exists = Assert.IsType<bool>(Assert.Single(ps.Invoke()).BaseObject);
        Assert.False(exists);
        Assert.Empty(ps.Streams.Error);

        _mediaReaderServiceMock.Verify(
            m => m.GetMediaFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PowerShell CreatePowerShell()
    {
        var asm = typeof(MediaCmdletProvider).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName!, asm.Location));
        initialSessionState.Providers.Add(new SessionStateProviderEntry("Media", typeof(MediaCmdletProvider), null));
        return PowerShell.Create(initialSessionState);
    }

    private static MediaFile CreateMediaFile(string path)
    {
        var format = new MediaFormat(
            Path: path,
            StreamCount: 3,
            Format: "matroska",
            FormatLongName: "Matroska",
            StartTime: 0,
            Duration: 1.0m,
            Size: 1024,
            BitRate: 1000,
            Tags: new Dictionary<string, string>(),
            Title: "Sample");

        var streams = new[]
        {
            new MediaStream("video", 0, "h264", "High", "H.264", new Dictionary<string, string>(), Language: null),
            new MediaStream("audio", 1, "aac", "LC", "AAC", new Dictionary<string, string> { ["title"] = "English" }, Language: "eng"),
            new MediaStream("subtitle", 2, "subrip", "", "SubRip", new Dictionary<string, string>(), Language: "eng"),
        };

        var chapters = new[]
        {
            new MediaChapter(0, 0, 10, new Dictionary<string, string>(), Title: "Intro"),
            new MediaChapter(1, 10, 20, new Dictionary<string, string>(), Title: "Main"),
        };

        return new MediaFile(path, format, chapters, streams, Raw: "{}");
    }
}
