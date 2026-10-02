using System;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class MediaConversionHelperProgressWriteTests
{
    [Fact]
    public void WriteCurrentItemProgress_WithEta_SetsSecondsRemainingAndPreservesStatus()
    {
        var io = new FakeCmdletIO();

        MediaConversionHelper.WriteCurrentItemProgress(
            io,
            "File Conversion",
            "Encoding",
            "out.mp4",
            42,
            TimeSpan.FromSeconds(30.2));

        var record = Assert.Single(io.ProgressRecords);
        Assert.Equal(ProgressActivityIds.CurrentItem, record.ActivityId);
        Assert.Equal(ProgressActivityIds.Main, record.ParentActivityId);
        Assert.Equal("File Conversion", record.Activity);
        Assert.Equal("Encoding", record.StatusDescription);
        Assert.Equal("out.mp4", record.CurrentOperation);
        Assert.Equal(42, record.PercentComplete);
        Assert.Equal(31, record.SecondsRemaining);
    }

    [Fact]
    public void WriteMainProgress_WithEta_SetsSecondsRemainingAndPreservesStatus()
    {
        var io = new FakeCmdletIO();

        MediaConversionHelper.WriteMainProgress(
            io,
            "Batch Conversion",
            "Working",
            15,
            TimeSpan.FromSeconds(0.1));

        var record = Assert.Single(io.ProgressRecords);
        Assert.Equal(ProgressActivityIds.Main, record.ActivityId);
        Assert.Equal("Batch Conversion", record.Activity);
        Assert.Equal("Working", record.StatusDescription);
        Assert.Equal(15, record.PercentComplete);
        Assert.Equal(1, record.SecondsRemaining);
    }

    [Fact]
    public void WriteProgress_WithoutEta_DoesNotSetSecondsRemaining()
    {
        var io = new FakeCmdletIO();

        MediaConversionHelper.WriteCurrentItemProgress(
            io,
            "File Conversion",
            "Encoding",
            percentComplete: 10);

        var record = Assert.Single(io.ProgressRecords);
        Assert.Equal(10, record.PercentComplete);
        Assert.Equal(-1, record.SecondsRemaining);
    }

    [Fact]
    public void WriteProgressCompleted_WritesCompletedMainAndCurrentItemRecords()
    {
        var io = new FakeCmdletIO();

        MediaConversionHelper.WriteProgressCompleted(io, "Batch Conversion", "File Conversion");

        Assert.Equal(2, io.ProgressRecords.Count);
        Assert.All(io.ProgressRecords, r => Assert.Equal(ProgressRecordType.Completed, r.RecordType));
        Assert.Equal(ProgressActivityIds.Main, io.ProgressRecords[0].ActivityId);
        Assert.Equal(ProgressActivityIds.CurrentItem, io.ProgressRecords[1].ActivityId);
    }

    [Fact]
    public void WriteMainProgress_WithPercent_UpdatesTerminalTitlePercent()
    {
        var io = new FakeCmdletIO { WindowTitle = "MF: Convert-MediaFiles" };

        MediaConversionHelper.WriteMainProgress(
            io,
            "Batch Conversion",
            "Working",
            42);

        Assert.Equal([42], io.ProgressTitlePercents);
        Assert.Equal("MF: Convert-MediaFiles (42%)", io.WindowTitle);
    }

    [Fact]
    public void WriteCurrentItemProgress_WithPercent_UpdatesTerminalTitlePercent()
    {
        var io = new FakeCmdletIO { WindowTitle = "MF: Convert-VideoFile" };

        MediaConversionHelper.WriteCurrentItemProgress(
            io,
            "File Conversion",
            "Encoding",
            "out.mp4",
            75);

        Assert.Equal([75], io.ProgressTitlePercents);
        Assert.Equal("MF: Convert-VideoFile (75%)", io.WindowTitle);
    }

    [Fact]
    public void WriteMainProgress_WithoutPercent_DoesNotUpdateTerminalTitle()
    {
        var io = new FakeCmdletIO { WindowTitle = "MF: Convert-MediaFiles" };

        MediaConversionHelper.WriteMainProgress(io, "Batch Conversion", "Working");

        Assert.Empty(io.ProgressTitlePercents);
        Assert.Equal("MF: Convert-MediaFiles", io.WindowTitle);
    }

    [Fact]
    public void WriteProgressCompleted_ClearsTerminalTitlePercent()
    {
        var io = new FakeCmdletIO { WindowTitle = "MF: Convert-MediaFiles" };
        MediaConversionHelper.WriteMainProgress(io, "Batch Conversion", "Working", 55);
        Assert.Equal("MF: Convert-MediaFiles (55%)", io.WindowTitle);

        MediaConversionHelper.WriteProgressCompleted(io, "Batch Conversion", "File Conversion");

        Assert.Equal(1, io.ClearProgressTitlePercentCount);
        Assert.Equal("MF: Convert-MediaFiles", io.WindowTitle);
    }

    [Theory]
    [InlineData("MF: Convert-MediaFiles", 0, "MF: Convert-MediaFiles (0%)")]
    [InlineData("MF: Convert-MediaFiles", 100, "MF: Convert-MediaFiles (100%)")]
    [InlineData("MF: Convert-MediaFiles (12%)", 34, "MF: Convert-MediaFiles (34%)")]
    [InlineData("MF: Convert-VideoFile: Encoding", 50, "MF: Convert-VideoFile: Encoding (50%)")]
    [InlineData("MF: Convert-MediaFiles", -5, "MF: Convert-MediaFiles (0%)")]
    [InlineData("MF: Convert-MediaFiles", 150, "MF: Convert-MediaFiles (100%)")]
    public void FormatTitleWithProgressPercent_AppendsOrReplacesPercent(
        string title,
        int percent,
        string expected)
    {
        Assert.Equal(expected, MediaConversionHelper.FormatTitleWithProgressPercent(title, percent));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("MF: Convert-MediaFiles", "MF: Convert-MediaFiles")]
    [InlineData("MF: Convert-MediaFiles (42%)", "MF: Convert-MediaFiles")]
    [InlineData("MF: Convert-VideoFile: Encoding (7%)", "MF: Convert-VideoFile: Encoding")]
    [InlineData("Title (not-a-percent%)", "Title (not-a-percent%)")]
    [InlineData("Title (%)", "Title (%)")]
    public void RemoveProgressPercentFromTitle_StripsTrailingPercentSuffix(string? title, string expected)
    {
        Assert.Equal(expected, MediaConversionHelper.RemoveProgressPercentFromTitle(title));
    }
}
