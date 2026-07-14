using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Providers;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Providers;

public class MediaCmdletProviderComponentTests : ComponentTestBase
{
    [Fact]
    public void Provider_WithSampleVideoRoot_ListsFormatStreamsAndChapters()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellWithMediaProvider();
        MountDrive(ps, "mf", SampleVideoPath);

        ps.Commands.Clear();
        ps.AddCommand("Get-ChildItem").AddParameter("Path", "mf:");
        var children = ps.Invoke().Select(r => r.BaseObject).ToArray();
        Assert.Empty(ps.Streams.Error);

        Assert.Contains(children, o => o is MediaFormat);
        Assert.Contains(children, o => o is MediaContainerItem { Name: "chapters" });
        Assert.Contains(children, o => o is MediaContainerItem { Name: "streams" });
    }

    [Fact]
    public void Provider_GetItem_Format_ReturnsProbedFormat()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellWithMediaProvider();
        MountDrive(ps, "mf", SampleVideoPath);

        ps.Commands.Clear();
        ps.AddCommand("Get-Item").AddParameter("Path", @"mf:\format");
        var results = ps.Invoke();
        Assert.Empty(ps.Streams.Error);

        var format = Assert.IsType<MediaFormat>(Assert.Single(results).BaseObject);
        Assert.False(string.IsNullOrWhiteSpace(format.Format));
        Assert.True(format.Duration >= 0);
        Assert.True(format.Size > 0);
        Assert.True(format.StreamCount >= 1);
    }

    [Fact]
    public void Provider_GetItem_VideoStream_ReturnsSampleH264Stream()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellWithMediaProvider();
        MountDrive(ps, "mf", SampleVideoPath);

        ps.Commands.Clear();
        ps.AddCommand("Get-Item").AddParameter("Path", @"mf:\streams\video\0");
        var results = ps.Invoke();
        Assert.Empty(ps.Streams.Error);

        var stream = Assert.IsType<MediaStream>(Assert.Single(results).BaseObject);
        Assert.Equal("video", stream.Type);
        Assert.Equal(0, stream.Index);
        Assert.Equal("h264", stream.Codec);
    }

    [Fact]
    public void Provider_GetItem_AbsoluteStreamIndex_MatchesVideoStream()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellWithMediaProvider();
        MountDrive(ps, "mf", SampleVideoPath);

        ps.Commands.Clear();
        ps.AddCommand("Get-Item").AddParameter("Path", @"mf:\streams\all\0");
        var results = ps.Invoke();
        Assert.Empty(ps.Streams.Error);

        var stream = Assert.IsType<MediaStream>(Assert.Single(results).BaseObject);
        Assert.Equal("video", stream.Type);
        Assert.Equal(0, stream.Index);
        Assert.Equal("h264", stream.Codec);
    }

    [Fact]
    public void Provider_GetChildItem_VideoStreams_ListsSingleStream()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellWithMediaProvider();
        MountDrive(ps, "mf", SampleVideoPath);

        ps.Commands.Clear();
        ps.AddCommand("Get-ChildItem").AddParameter("Path", @"mf:\streams\video");
        var streams = ps.Invoke().Select(r => r.BaseObject).OfType<MediaStream>().ToArray();
        Assert.Empty(ps.Streams.Error);

        Assert.Single(streams);
        Assert.Equal("h264", streams[0].Codec);
    }

    [Fact]
    public void Provider_WithAssetsDirectoryRoot_ListsSampleMediaFile()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var directory = CreateTempDirectory();
        var mediaCopy = Path.Combine(directory, "sample-1s.mkv");
        File.Copy(SampleVideoPath, mediaCopy);

        using var ps = CreatePowerShellWithMediaProvider();
        MountDrive(ps, "mf", directory);

        ps.Commands.Clear();
        ps.AddCommand("Get-ChildItem").AddParameter("Path", "mf:");
        var children = ps.Invoke().Select(r => r.BaseObject).ToArray();
        Assert.Empty(ps.Streams.Error);

        var mediaFile = Assert.Single(children.OfType<MediaFile>());
        Assert.True(
            string.Equals(mediaFile.Path, mediaCopy, StringComparison.OrdinalIgnoreCase),
            $"Expected {mediaCopy}, got {mediaFile.Path}");
        Assert.NotEmpty(mediaFile.Streams);
    }

    [Fact]
    public void Provider_InvalidMedia_WritesError()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellWithMediaProvider();
        MountDrive(ps, "mf", InvalidMediaPath);

        ps.Commands.Clear();
        ps.AddCommand("Get-Item").AddParameter("Path", @"mf:\format");
        var results = ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
    }

    private static void MountDrive(PowerShell ps, string name, string root)
    {
        ps.AddCommand("New-PSDrive")
            .AddParameter("Name", name)
            .AddParameter("PSProvider", "Media")
            .AddParameter("Root", root);
        ps.Invoke();
        Assert.Empty(ps.Streams.Error);
    }
}
