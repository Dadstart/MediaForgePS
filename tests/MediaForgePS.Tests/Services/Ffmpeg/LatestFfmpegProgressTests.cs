using System;
using System.Linq;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class LatestFfmpegProgressTests
{
    [Fact]
    public void Latest_InitiallyNull()
    {
        var progress = new LatestFfmpegProgress();

        Assert.Null(progress.Latest);
    }

    [Fact]
    public void Report_UpdatesLatest()
    {
        var progressive = new LatestFfmpegProgress();
        var first = new FfmpegProgress(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), 10, null);
        var second = new FfmpegProgress(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), 50, TimeSpan.FromSeconds(4));

        progressive.Report(first);
        Assert.Same(first, progressive.Latest);

        progressive.Report(second);
        Assert.Same(second, progressive.Latest);
        Assert.Equal(50, progressive.Latest!.PercentComplete);
    }

    [Fact]
    public async Task Report_FromMultipleThreads_KeepsAReportedValue()
    {
        var progressive = new LatestFfmpegProgress();
        var reports = new FfmpegProgress[50];
        for (var i = 0; i < reports.Length; i++)
            reports[i] = new FfmpegProgress(TimeSpan.FromSeconds(i), TimeSpan.FromSeconds(100), i, null);

        await Task.WhenAll(reports.Select(report => Task.Run(() => progressive.Report(report))));

        Assert.NotNull(progressive.Latest);
        Assert.Contains(progressive.Latest, reports);
    }
}
