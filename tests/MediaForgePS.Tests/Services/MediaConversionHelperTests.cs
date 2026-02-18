using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class MediaConversionHelperTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.0 GB")]
    public void FormatByteCount_ReturnsExpectedValue(long bytes, string expected)
    {
        var result = MediaConversionHelper.FormatByteCount(bytes);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, 45, "45s")]
    [InlineData(0, 2, 30, "2m 30s")]
    [InlineData(1, 5, 0, "1h 5m 0s")]
    public void FormatTimespan_ReturnsExpectedValue(int hours, int minutes, int seconds, string expected)
    {
        var time = new TimeSpan(hours, minutes, seconds);

        var result = MediaConversionHelper.FormatTimespan(time);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildBatchProgressStatus_WithTotalBytes_UsesByteBasedPercentAndStatus()
    {
        var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
            2,
            5,
            "sample.mkv",
            1048576,
            2097152);

        Assert.Equal(50, percent);
        Assert.Equal("File 2 of 5 (50%) — 1.0 MB / 2.0 MB — sample.mkv", status);
    }

    [Fact]
    public void BuildBatchProgressStatus_WithoutTotalBytes_FallsBackToCountBasedPercent()
    {
        var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(
            2,
            5,
            "sample.mkv",
            0,
            0);

        Assert.Equal(40, percent);
        Assert.Equal("File 2 of 5 (40%) — sample.mkv", status);
    }

    [Fact]
    public void BuildCountBasedProgressStatus_WithTotalFiles_ReturnsExpectedStatus()
    {
        var (status, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(
            3,
            5,
            "episode.mkv");

        Assert.Equal(60, percent);
        Assert.Equal("File 3 of 5 (60%) — episode.mkv", status);
    }

    [Fact]
    public void BuildCountBasedProgressStatus_WithZeroTotalFiles_ReturnsZeroPercent()
    {
        var (status, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(
            1,
            0,
            "episode.mkv");

        Assert.Equal(0, percent);
        Assert.Equal("File 1 of 0 (0%) — episode.mkv", status);
    }

    [Fact]
    public void CalculateRemainingTime_WithValidStats_ReturnsExpectedEstimate()
    {
        var stats = new[]
        {
            (FileSizeBytes: 1000L, ProcessingTime: TimeSpan.FromSeconds(10)),
            (FileSizeBytes: 2000L, ProcessingTime: TimeSpan.FromSeconds(20))
        };

        var result = MediaConversionHelper.CalculateRemainingTime(3000, stats);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromSeconds(30), result.Value);
    }

    [Fact]
    public void CalculateRemainingTime_WithNoStats_ReturnsNull()
    {
        var result = MediaConversionHelper.CalculateRemainingTime(
            1000,
            []);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1000, 0)]
    [InlineData(-1, 10)]
    public void CalculateRemainingTime_WithInvalidStatsTotals_ReturnsNull(long totalBytes, int totalSeconds)
    {
        var stats = new[]
        {
            (FileSizeBytes: totalBytes, ProcessingTime: TimeSpan.FromSeconds(totalSeconds))
        };

        var result = MediaConversionHelper.CalculateRemainingTime(1000, stats);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void CalculateRemainingTime_WithNoRemainingBytes_ReturnsNull(long remainingBytes)
    {
        var stats = new[]
        {
            (FileSizeBytes: 1000L, ProcessingTime: TimeSpan.FromSeconds(10))
        };

        var result = MediaConversionHelper.CalculateRemainingTime(remainingBytes, stats);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("libx265", true)]
    [InlineData("x265", true)]
    [InlineData("LIBX265", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("libx264", false)]
    [InlineData("h264", false)]
    public void IsX265Codec_ReturnsExpectedValue(string codec, bool expected)
    {
        var result = MediaConversionHelper.IsX265Codec(codec);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildX265Arguments_WithNoParams_ReturnsNull()
    {
        var result = MediaConversionHelper.BuildX265Arguments(null, "libx265");

        Assert.Null(result);
    }

    [Fact]
    public void BuildX265Arguments_WithWhitespaceParams_ReturnsNull()
    {
        var result = MediaConversionHelper.BuildX265Arguments("   ", "libx265");

        Assert.Null(result);
    }

    [Fact]
    public void BuildX265Arguments_WithX265Params_ReturnsX265Args()
    {
        var result = MediaConversionHelper.BuildX265Arguments("psy-rd=2.0", "libx265");

        var expected = new[] { "-x265-params", "psy-rd=2.0" };
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildX265Arguments_WithNonX265Codec_ReturnsNull()
    {
        var result = MediaConversionHelper.BuildX265Arguments("bframes=8", "libx264");

        Assert.Null(result);
    }

    [Fact]
    public void CreateSimpleProgressRecord_WithDefaults_SetsExpectedProperties()
    {
        var record = MediaConversionHelper.CreateSimpleProgressRecord(
            1,
            "Batch Conversion",
            "Processing");

        Assert.Equal(1, record.ActivityId);
        Assert.Equal("Batch Conversion", record.Activity);
        Assert.Equal("Processing", record.StatusDescription);
        Assert.Equal(ProgressRecordType.Processing, record.RecordType);
        Assert.Equal(-1, record.ParentActivityId);
    }

    [Fact]
    public void CreateSimpleProgressRecord_WithOptionalValues_SetsParentAndPercent()
    {
        var record = MediaConversionHelper.CreateSimpleProgressRecord(
            3,
            "Batch Conversion",
            "Halfway",
            50,
            7,
            ProgressRecordType.Completed);

        Assert.Equal(7, record.ParentActivityId);
        Assert.Equal(50, record.PercentComplete);
        Assert.Equal(ProgressRecordType.Completed, record.RecordType);
    }

    [Fact]
    public void CreateNestedProgressRecord_SetsParentAndOperation()
    {
        var record = MediaConversionHelper.CreateNestedProgressRecord(
            2,
            "File Conversion",
            "Encoding",
            1,
            "test.mp4",
            -1,
            ProgressRecordType.Processing);

        Assert.Equal(2, record.ActivityId);
        Assert.Equal(1, record.ParentActivityId);
        Assert.Equal("test.mp4", record.CurrentOperation);
        Assert.Equal(-1, record.PercentComplete);
        Assert.Equal(ProgressRecordType.Processing, record.RecordType);
    }

    [Fact]
    public void CreateNestedProgressRecord_WithWhitespaceOperation_DoesNotSetCurrentOperation()
    {
        var record = MediaConversionHelper.CreateNestedProgressRecord(
            2,
            "File Conversion",
            "Encoding",
            1,
            "   ",
            10,
            ProgressRecordType.Processing);

        Assert.True(string.IsNullOrWhiteSpace(record.CurrentOperation));
    }
}
