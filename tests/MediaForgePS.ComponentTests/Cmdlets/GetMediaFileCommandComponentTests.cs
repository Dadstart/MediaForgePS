using System.IO;
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
    public void GetMediaFile_WithValidSampleVideo_ReturnsMediaFileWithFormatAndStreams()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        using var ps = CreatePowerShellFor<GetMediaFileCommand>("Get-MediaFile");
        ps.AddCommand("Get-MediaFile").AddParameter("Path", SampleVideoPath);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var mediaFile = Assert.IsType<MediaFile>(results[0].BaseObject);
        Assert.NotNull(mediaFile.Format);
        Assert.True(mediaFile.Format.Size > 0);
        Assert.False(string.IsNullOrEmpty(mediaFile.Format.FormatLongName));
        Assert.NotNull(mediaFile.Streams);
        Assert.NotEmpty(mediaFile.Streams);
        Assert.NotNull(mediaFile.Chapters);
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

    [Fact]
    public void GetMediaFile_WithNonExistentPath_WritesErrorAndReturnsNoOutput()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var nonExistentPath = Path.Combine(AssetsRoot, "nonexistent.mkv");
        Assert.False(File.Exists(nonExistentPath));

        using var ps = CreatePowerShellFor<GetMediaFileCommand>("Get-MediaFile");
        ps.AddCommand("Get-MediaFile").AddParameter("Path", nonExistentPath);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(results);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void GetMediaFile_WithTwoValidPaths_ReturnsTwoMediaFiles()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var allResults = new List<object>();
        var allErrors = new List<object>();
        using (var ps = CreatePowerShellFor<GetMediaFileCommand>("Get-MediaFile"))
        {
            foreach (var path in new[] { SampleVideoPath, SampleVideoPath })
            {
                ps.Commands.Clear();
                ps.AddCommand("Get-MediaFile").AddParameter("Path", path);
                var run = ps.Invoke().ToList();
                allResults.AddRange(run.Select(r => r.BaseObject));
                allErrors.AddRange(ps.Streams.Error.ReadAll());
            }
        }

        Assert.Empty(allErrors);
        Assert.Equal(2, allResults.Count);
        Assert.IsType<MediaFile>(allResults[0]);
        Assert.IsType<MediaFile>(allResults[1]);
    }

    [Fact]
    public void GetMediaFile_WithValidThenInvalidPath_ReturnsOneMediaFileAndWritesError()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var allResults = new List<object>();
        var allErrors = new List<object>();
        using (var ps = CreatePowerShellFor<GetMediaFileCommand>("Get-MediaFile"))
        {
            foreach (var path in new[] { SampleVideoPath, InvalidMediaPath })
            {
                ps.Commands.Clear();
                ps.AddCommand("Get-MediaFile").AddParameter("Path", path);
                var run = ps.Invoke().ToList();
                allResults.AddRange(run.Select(r => r.BaseObject));
                allErrors.AddRange(ps.Streams.Error.ReadAll());
            }
        }

        Assert.Single(allResults);
        Assert.IsType<MediaFile>(allResults[0]);
        Assert.NotEmpty(allErrors);
    }
}

