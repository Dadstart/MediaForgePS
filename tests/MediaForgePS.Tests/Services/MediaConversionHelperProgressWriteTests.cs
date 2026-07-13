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
}
