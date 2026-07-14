using System.Diagnostics;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class ConvertMediaFileAdvancedCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void ConvertMediaFileAdvanced_WithValidSampleVideo_WritesConversionResultWithSizeAndDuration()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CreateSampleVideoWithSilentAudio("advanced-in.mkv");
        var outputDir = CreateTempDirectory();
        var expectedOutput = Path.Combine(outputDir, "advanced-out.mp4");
        var settings = new ConstantRateVideoEncodingSettings(
            "libx264",
            "veryfast",
            "high",
            "film",
            28,
            "yuv420p");
        var audioMappings = new AudioTrackMapping[]
        {
            new EncodeAudioTrackMapping("Stereo", 0, 0, 0, "aac", 128, 2)
        };

        using var ps = CreatePowerShellFor<ConvertMediaFileAdvancedCommand>("Convert-MediaFileAdvanced");
        ps.AddCommand("Convert-MediaFileAdvanced")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", expectedOutput)
            .AddParameter("VideoEncodingSettings", settings)
            .AddParameter("AudioTrackMappings", audioMappings);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<MediaConversionResult>(Assert.Single(results).BaseObject);
        AssertSuccessfulConversionResult(result, inputPath, expectedOutput);
    }

    private string CreateSampleVideoWithSilentAudio(string fileName)
    {
        var directory = CreateTempDirectory();
        var destination = Path.Combine(directory, fileName);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments =
                    $"-y -i \"{SampleVideoPath}\" -f lavfi -i anullsrc=channel_layout=stereo:sample_rate=48000 " +
                    $"-c:v copy -c:a aac -b:a 128k -shortest \"{destination}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        Assert.True(process.Start(), "Failed to start ffmpeg to create sample with audio.");
        Assert.True(process.WaitForExit(30_000), "ffmpeg timed out creating sample with audio.");
        Assert.Equal(0, process.ExitCode);
        Assert.True(File.Exists(destination));

        return destination;
    }
}
