using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class GetMediaFileCommandComponentTests : ComponentTestBase
{
    [Fact]
    public void GetMediaFile_WithValidSampleVideo_ReturnsMediaFileWithExpectedProperties()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellFor<GetMediaFileCommand>("Get-MediaFile");
        ps.AddCommand("Get-MediaFile").AddParameter("Path", SampleVideoPath);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Single(results);
        var mediaFile = Assert.IsType<MediaFile>(results[0].BaseObject);
        Assert.True(
            string.Equals(mediaFile.Path, SampleVideoPath, StringComparison.OrdinalIgnoreCase),
            $"Path mismatch: expected {SampleVideoPath}, got {mediaFile.Path}");
        Assert.NotNull(mediaFile.Format);
        Assert.True(mediaFile.Format.Duration >= 0);
        Assert.False(string.IsNullOrEmpty(mediaFile.Format.Format));
    }

    [Fact]
    public void GetMediaFile_WithInvalidMedia_WritesErrorAndReturnsNoOutput()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellFor<GetMediaFileCommand>("Get-MediaFile");
        ps.AddCommand("Get-MediaFile").AddParameter("Path", InvalidMediaPath);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
    }
}

