using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Providers;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Providers;

public class MediaDriveInfoTests : IDisposable
{
    private readonly InitialSessionState _sessionState;
    private readonly PowerShell _powerShell;

    public MediaDriveInfoTests()
    {
        _sessionState = InitialSessionState.CreateDefault();
        _powerShell = PowerShell.Create(_sessionState);
    }

    public void Dispose()
    {
        _powerShell.Dispose();
    }
    [Fact]
    public void SetCachedMediaFile_EvictsLeastRecentlyUsed_WhenCapacityExceeded()
    {
        var drive = CreateDrive(cacheCapacity: 2);
        var first = CreateMediaFile(@"C:\media\first.mkv");
        var second = CreateMediaFile(@"C:\media\second.mkv");
        var third = CreateMediaFile(@"C:\media\third.mkv");

        drive.SetCachedMediaFile(first.Path, first);
        drive.SetCachedMediaFile(second.Path, second);

        Assert.True(drive.TryGetCachedMediaFile(first.Path, out _));
        Assert.True(drive.TryGetCachedMediaFile(second.Path, out _));

        drive.SetCachedMediaFile(third.Path, third);

        Assert.False(drive.TryGetCachedMediaFile(first.Path, out _));
        Assert.True(drive.TryGetCachedMediaFile(second.Path, out _));
        Assert.True(drive.TryGetCachedMediaFile(third.Path, out _));
    }

    [Fact]
    public void TryGetCachedMediaFile_RefreshesRecency()
    {
        var drive = CreateDrive(cacheCapacity: 2);
        var first = CreateMediaFile(@"C:\media\first.mkv");
        var second = CreateMediaFile(@"C:\media\second.mkv");
        var third = CreateMediaFile(@"C:\media\third.mkv");

        drive.SetCachedMediaFile(first.Path, first);
        drive.SetCachedMediaFile(second.Path, second);
        Assert.True(drive.TryGetCachedMediaFile(first.Path, out _));

        drive.SetCachedMediaFile(third.Path, third);

        Assert.True(drive.TryGetCachedMediaFile(first.Path, out _));
        Assert.False(drive.TryGetCachedMediaFile(second.Path, out _));
        Assert.True(drive.TryGetCachedMediaFile(third.Path, out _));
    }

    [Fact]
    public void ClearCache_RemovesAllEntries()
    {
        var drive = CreateDrive(cacheCapacity: 2);
        var mediaFile = CreateMediaFile(@"C:\media\sample.mkv");

        drive.SetCachedMediaFile(mediaFile.Path, mediaFile);
        drive.ClearCache();

        Assert.False(drive.TryGetCachedMediaFile(mediaFile.Path, out _));
    }

    private MediaDriveInfo CreateDrive(int cacheCapacity)
    {
        var root = Path.GetTempPath();
        _powerShell.AddCommand("New-PSDrive")
            .AddParameter("Name", "mf")
            .AddParameter("PSProvider", "FileSystem")
            .AddParameter("Root", root);
        var driveInfo = _powerShell.Invoke().Select(r => r.BaseObject).OfType<PSDriveInfo>().Single();
        _powerShell.Commands.Clear();
        return new MediaDriveInfo(driveInfo, cacheCapacity);
    }

    private static MediaFile CreateMediaFile(string path)
    {
        return new MediaFile(
            path,
            new MediaFormat(path, 1, "matroska", "Matroska", 0, 1, 1024, 1024, new Dictionary<string, string>()),
            [],
            []);
    }
}
