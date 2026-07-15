using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Services;

/// <summary>
/// Exercises real <see cref="ExecutableService"/> + <see cref="FfmpegService"/> with paths that contain spaces.
/// </summary>
public class FfmpegServiceExecutableServiceIntegrationTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public async Task ConvertAsync_WithSpacedInputAndOutputPaths_RemuxesViaArgumentList()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var workDir = CreateTempDirectory();
        var inputDir = Path.Combine(workDir, "source media");
        var outputDir = Path.Combine(workDir, "export folder");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        var inputPath = Path.Combine(inputDir, "sample video.mkv");
        var outputPath = Path.Combine(outputDir, "converted video.mp4");
        File.Copy(SampleVideoPath, inputPath);

        var executable = new ExecutableService(NullLogger<ExecutableService>.Instance);
        var ffprobe = new FfprobeService(executable, NullLogger<FfprobeService>.Instance);
        var ffmpeg = new FfmpegService(executable, ffprobe, NullLogger<FfmpegService>.Instance);

        await ffmpeg.ConvertAsync(
            inputPath,
            outputPath,
            arguments: ["-c", "copy"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact(Timeout = 60_000)]
    public async Task ConvertAsync_WithProgressAndSpacedPaths_StreamsStdoutThroughExecutableService()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var workDir = CreateTempDirectory();
        var inputPath = Path.Combine(workDir, "progress input.mkv");
        var outputPath = Path.Combine(workDir, "progress output.mp4");
        File.Copy(SampleVideoPath, inputPath);

        var reports = new List<FfmpegProgress>();
        var executable = new ExecutableService(NullLogger<ExecutableService>.Instance);
        var ffprobe = new FfprobeService(executable, NullLogger<FfprobeService>.Instance);
        var ffmpeg = new FfmpegService(executable, ffprobe, NullLogger<FfmpegService>.Instance);

        await ffmpeg.ConvertAsync(
            inputPath,
            outputPath,
            arguments: ["-c:v", "libx264", "-preset", "ultrafast", "-crf", "30", "-an"],
            progress: new CollectingProgress(reports),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(outputPath));
        Assert.NotEmpty(reports);
        Assert.True(reports.Exists(report => report.PercentComplete >= 0));
    }

    private sealed class CollectingProgress(List<FfmpegProgress> reports) : IProgress<FfmpegProgress>
    {
        public void Report(FfmpegProgress value) => reports.Add(value);
    }
}
