using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
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
    [InlineData(0, 0, 45, "00:45")]
    [InlineData(0, 2, 30, "02:30")]
    [InlineData(1, 5, 0, "1:05:00")]
    [InlineData(12, 3, 4, "12:03:04")]
    public void FormatTimespan_ReturnsExpectedValue(int hours, int minutes, int seconds, string expected)
    {
        var time = new TimeSpan(hours, minutes, seconds);

        var result = MediaConversionHelper.FormatTimespan(time);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1000, 400, 60.0)]
    [InlineData(1000, 1000, 0.0)]
    [InlineData(1000, 1200, -20.0)]
    [InlineData(0, 100, null)]
    public void CalculateSizeReductionPercent_ReturnsExpectedValue(long inputBytes, long outputBytes, double? expected)
    {
        var result = MediaConversionHelper.CalculateSizeReductionPercent(inputBytes, outputBytes);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(60.0, "60% smaller")]
    [InlineData(-20.0, "20% larger")]
    [InlineData(0.0, "same size")]
    [InlineData(null, "n/a")]
    public void FormatSizeReduction_ReturnsExpectedValue(double? percent, string expected)
    {
        var result = MediaConversionHelper.FormatSizeReduction(percent);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatConversionResultLine_WithSuccess_IncludesOutputPathSizeAndDuration()
    {
        var result = new MediaConversionResult(
            @"C:\in.mkv",
            @"C:\out.mp4",
            MediaConversionResult.CompletedStatus,
            1 << 20,
            512 * 1024,
            50.0,
            TimeSpan.FromSeconds(95));

        var line = MediaConversionHelper.FormatConversionResultLine(result);

        Assert.Contains(@"C:\out.mp4", line, StringComparison.Ordinal);
        Assert.Contains("50% smaller", line, StringComparison.Ordinal);
        Assert.Contains("1.0 MB → 512.0 KB", line, StringComparison.Ordinal);
        Assert.Contains("01:35", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatConversionResultLine_WithFailure_IncludesInputPathAndStatus()
    {
        var result = new MediaConversionResult(
            @"C:\in.mkv",
            @"C:\in.mkv",
            "Failed to read media metadata.",
            100,
            0,
            null,
            TimeSpan.FromSeconds(1));

        var line = MediaConversionHelper.FormatConversionResultLine(result);

        Assert.Equal(@"C:\in.mkv — Failed to read media metadata.", line);
    }

    [Fact]
    public void CreateConversionResult_WithExistingFiles_ComputesSizeReduction()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var inputPath = Path.Combine(tempDir, "in.bin");
            var outputPath = Path.Combine(tempDir, "out.bin");
            File.WriteAllBytes(inputPath, new byte[1000]);
            File.WriteAllBytes(outputPath, new byte[400]);

            var result = MediaConversionHelper.CreateConversionResult(
                inputPath,
                outputPath,
                true,
                "Success",
                TimeSpan.FromSeconds(12));

            Assert.Equal(1000, result.InputSizeBytes);
            Assert.Equal(400, result.OutputSizeBytes);
            Assert.Equal(60.0, result.SizeReductionPercent);
            Assert.Equal(TimeSpan.FromSeconds(12), result.ProcessingTime);
            Assert.Equal(inputPath, result.FilePath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void BuildEncodeProgressStatus_WithTotalDuration_IncludesMmSsRange()
    {
        var progress = new FfmpegProgress(
            TimeSpan.FromSeconds(152),
            TimeSpan.FromMinutes(42) + TimeSpan.FromSeconds(15),
            6,
            null);

        var result = MediaConversionHelper.BuildEncodeProgressStatus(
            "Encoding to libx265 (medium preset)",
            progress);

        Assert.Equal("Encoding to libx265 (medium preset) — 02:32 / 42:15", result);
    }

    [Fact]
    public void BuildEncodeProgressStatus_WithoutTotalDuration_IncludesOutTimeOnly()
    {
        var progress = new FfmpegProgress(TimeSpan.FromSeconds(5), TimeSpan.Zero, 0, null);

        var result = MediaConversionHelper.BuildEncodeProgressStatus("Encoding", progress);

        Assert.Equal("Encoding — 00:05", result);
    }

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(0.0, true)]
    [InlineData(0.4, true)]
    [InlineData(1.1, false)]
    public void IsEncodeFinishing_ReturnsExpectedValue(double etaSeconds, bool expected)
    {
        var progress = new FfmpegProgress(
            TimeSpan.FromSeconds(99),
            TimeSpan.FromSeconds(100),
            99,
            TimeSpan.FromSeconds(etaSeconds));

        Assert.Equal(expected, MediaConversionHelper.IsEncodeFinishing(progress));
    }

    [Fact]
    public void IsEncodeFinishing_WithNullEta_ReturnsFalse()
    {
        var progress = new FfmpegProgress(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), 10, null);

        Assert.False(MediaConversionHelper.IsEncodeFinishing(progress));
    }

    [Fact]
    public void BuildEncodeProgressDisplay_WhenFinishing_ReturnsSpinnerStatusWithoutEta()
    {
        var progress = new FfmpegProgress(
            TimeSpan.FromSeconds(99),
            TimeSpan.FromSeconds(100),
            99,
            TimeSpan.FromSeconds(1));
        var spinner = new[] { "|", "/", "-", "\\" };
        var spinnerIndex = 0;

        var (status, eta) = MediaConversionHelper.BuildEncodeProgressDisplay(
            "Encoding to libx265 (medium preset)",
            progress,
            spinner,
            ref spinnerIndex);

        Assert.Equal("finishing |", status);
        Assert.Null(eta);
        Assert.Equal(1, spinnerIndex);
    }

    [Fact]
    public void BuildEncodeProgressDisplay_WhenNotFinishing_ReturnsMmSsStatusAndEta()
    {
        var progress = new FfmpegProgress(
            TimeSpan.FromSeconds(25),
            TimeSpan.FromSeconds(100),
            25,
            TimeSpan.FromSeconds(12));
        var spinner = new[] { "|", "/", "-", "\\" };
        var spinnerIndex = 0;

        var (status, eta) = MediaConversionHelper.BuildEncodeProgressDisplay(
            "Encoding",
            progress,
            spinner,
            ref spinnerIndex);

        Assert.Equal("Encoding — 00:25 / 01:40", status);
        Assert.Equal(TimeSpan.FromSeconds(12), eta);
        Assert.Equal(0, spinnerIndex);
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

    [Theory]
    [InlineData("nvenc", "hevc_nvenc")]
    [InlineData("x264", "libx264")]
    [InlineData(null, "libx265")]
    public void CreateDefaultVideoEncodingSettings_ReturnsExpectedCodec(string? encoder, string expectedCodec)
    {
        var settings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(encoder);

        Assert.Equal(expectedCodec, settings.Codec);
    }

    [Fact]
    public void CreateAutomaticAudioTrackMappings_WithDtsAndSixChannelAac_SwapsOrder()
    {
        var mappings = MediaConversionHelper.CreateAutomaticAudioTrackMappings(
        [
            CreateAudioStream(1, "dts", 6, "DTS 5.1"),
            CreateAudioStream(2, "aac", 6, "AAC 5.1")
        ]);

        Assert.Equal(2, mappings.Length);
        var first = Assert.IsType<EncodeAudioTrackMapping>(mappings[0]);
        var second = Assert.IsType<CopyAudioTrackMapping>(mappings[1]);

        Assert.Equal(0, first.DestinationIndex);
        Assert.Equal(1, second.DestinationIndex);
    }

    [Fact]
    public void BuildItemsWithSizes_WithExistingAndMissingFiles_ComputesTotalBytes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_MediaConversionHelper_" + Guid.NewGuid().ToString("N"));
        var existingPath = Path.Combine(tempDir, "existing.txt");
        var missingPath = Path.Combine(tempDir, "missing.txt");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(existingPath, [1, 2, 3, 4]);

            var entries = MediaConversionHelper.BuildItemsWithSizes(
                [existingPath, missingPath],
                static path => path,
                out var totalBytes);

            Assert.Equal(4, totalBytes);
            Assert.Equal(2, entries.Count);
            Assert.Equal(existingPath, entries[0].Item);
            Assert.Equal(4, entries[0].Size);
            Assert.Equal(missingPath, entries[1].Item);
            Assert.Equal(0, entries[1].Size);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SelectPreferredAudioStreams_WithEnglishAudio_PrefersEnglishOnly()
    {
        var selection = MediaConversionHelper.SelectPreferredAudioStreams(
        [
            CreateAudioStream(1, "aac", 2, language: "spa"),
            CreateAudioStream(2, "aac", 2, language: "eng"),
            CreateAudioStream(3, "aac", 6, language: "eng")
        ]);

        Assert.Equal(3, selection.TotalAudioStreamCount);
        Assert.Equal(2, selection.EnglishAudioStreamCount);
        Assert.Equal(2, selection.SelectedStreams.Count);
        Assert.All(selection.SelectedStreams, stream => Assert.Equal("eng", stream.Language));
    }

    [Fact]
    public void SelectPreferredAudioStreams_WithoutEnglishAudio_UsesAllAudio()
    {
        var selection = MediaConversionHelper.SelectPreferredAudioStreams(
        [
            CreateAudioStream(1, "aac", 2, language: "spa"),
            CreateAudioStream(2, "aac", 6, language: "fra")
        ]);

        Assert.Equal(2, selection.TotalAudioStreamCount);
        Assert.Equal(0, selection.EnglishAudioStreamCount);
        Assert.Equal(2, selection.SelectedStreams.Count);
    }

    [Fact]
    public void SelectPreferredAudioStreams_WithNoAudio_ReturnsEmptySelection()
    {
        var selection = MediaConversionHelper.SelectPreferredAudioStreams(Array.Empty<MediaStream>());

        Assert.Equal(0, selection.TotalAudioStreamCount);
        Assert.Equal(0, selection.EnglishAudioStreamCount);
        Assert.Empty(selection.SelectedStreams);
    }

    [Fact]
    public void BuildConversionFailureStatusMessage_WithExitCodeAndErrorOutput_ReturnsExpectedMessage()
    {
        var ex = new FfmpegConversionException(
            "failed",
            "in.mkv",
            "out.mp4",
            1,
            "first line\nsecond line");

        var message = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);

        Assert.Equal("Conversion failed (exit code: 1): first line", message);
    }

    [Fact]
    public void RunConversionWithProgress_ReportsInitialStatusThenCompletes()
    {
        var updates = new List<MediaConversionHelper.EncodeProgressUpdate>();
        using var started = new ManualResetEventSlim(false);

        MediaConversionHelper.RunConversionWithProgress(
            (progress, _) =>
            {
                started.Set();
                Thread.Sleep(80);
            },
            "Encoding to libx265 (slow preset)",
            "out.mp4",
            updates.Add,
            CancellationToken.None,
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.NotEmpty(updates);
        Assert.Equal("Encoding to libx265 (slow preset)", updates[0].Status);
        Assert.Equal("out.mp4", updates[0].CurrentOperation);
        Assert.Equal(0, updates[0].PercentComplete);
        Assert.True(started.IsSet);
    }

    [Fact]
    public void RunConversionWithProgress_WithProgressReports_UsesPercentAndStatus()
    {
        var updates = new List<MediaConversionHelper.EncodeProgressUpdate>();
        using var reported = new ManualResetEventSlim(false);

        MediaConversionHelper.RunConversionWithProgress(
            (progress, _) =>
            {
                progress.Report(new FfmpegProgress(
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(100),
                    30,
                    TimeSpan.FromSeconds(20)));
                Assert.True(reported.Wait(TimeSpan.FromSeconds(2)));
            },
            "Encoding",
            "out.mp4",
            update =>
            {
                updates.Add(update);
                if (update.PercentComplete == 30)
                    reported.Set();
            },
            CancellationToken.None,
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.Contains(updates, u =>
            u.PercentComplete == 30 &&
            u.Status.Contains("00:30 / 01:40", StringComparison.Ordinal) &&
            u.Eta == TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void RunConversionWithProgress_InvokesBatchProgressCallback()
    {
        var batchTicks = 0;
        using var allowFinish = new ManualResetEventSlim(false);

        MediaConversionHelper.RunConversionWithProgress(
            (_, _) => Assert.True(allowFinish.Wait(TimeSpan.FromSeconds(3))),
            "Encoding",
            "out.mp4",
            _ => { },
            CancellationToken.None,
            reportBatchProgress: () =>
            {
                Interlocked.Increment(ref batchTicks);
                allowFinish.Set();
            },
            pollInterval: TimeSpan.FromMilliseconds(10),
            batchUpdateInterval: TimeSpan.FromMilliseconds(20));

        Assert.True(batchTicks >= 1);
    }

    [Fact]
    public void RunConversionWithProgress_PropagatesConversionException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MediaConversionHelper.RunConversionWithProgress(
                (_, _) => throw new InvalidOperationException("encode failed"),
                "Encoding",
                "out.mp4",
                _ => { },
                CancellationToken.None));
    }

    [Fact]
    public void RunConversionWithProgress_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        using var convertStarted = new ManualResetEventSlim(false);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            MediaConversionHelper.RunConversionWithProgress(
                (_, _) =>
                {
                    convertStarted.Set();
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                },
                "Encoding",
                "out.mp4",
                _ =>
                {
                    if (convertStarted.IsSet)
                        cts.Cancel();
                },
                cts.Token,
                pollInterval: TimeSpan.FromMilliseconds(10)));
    }

    private static MediaStream CreateAudioStream(int index, string codec, int channels, string? title = null, string language = "eng")
    {
        var tags = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(title))
            tags["title"] = title;

        var rawJson = $@"{{
            ""index"": {index},
            ""codec_name"": ""{codec}"",
            ""codec_type"": ""audio"",
            ""channels"": {channels},
            ""tags"": {{}}
        }}";

        return new MediaStream(
            "audio",
            index,
            codec,
            string.Empty,
            string.Empty,
            tags,
            TimeSpan.Zero,
            language,
            rawJson);
    }
}
