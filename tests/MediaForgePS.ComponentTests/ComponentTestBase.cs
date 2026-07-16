using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Providers;
using Xunit;
using Xunit.Sdk;

namespace Dadstart.Labs.MediaForge.ComponentTests;

public abstract class ComponentTestBase : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    protected string AssetsRoot { get; }

    protected string SampleVideoPath =>
        Path.Combine(AssetsRoot, "sample-1s.mkv");

    protected string InvalidMediaPath =>
        Path.Combine(AssetsRoot, "invalid-media.mkv");

    protected ComponentTestBase()
    {
        AssetsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets");
    }

    protected string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS.ComponentTests");
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);

        return directory;
    }

    /// <summary>
    /// Copies the sample MKV into a temp directory under a new file name and returns the destination path.
    /// </summary>
    protected string CopySampleVideoAs(string fileName)
    {
        var directory = CreateTempDirectory();
        var destination = Path.Combine(directory, fileName);
        File.Copy(SampleVideoPath, destination);
        return destination;
    }

    /// <summary>
    /// Creates a short sample with a silent stereo AAC track tagged as English.
    /// </summary>
    protected string CreateSampleVideoWithEnglishAudio(string fileName)
    {
        var directory = CreateTempDirectory();
        var destination = Path.Combine(directory, fileName);

        RunFfmpeg(
            [
                "-y",
                "-i", SampleVideoPath,
                "-f", "lavfi",
                "-i", "anullsrc=channel_layout=stereo:sample_rate=48000",
                "-c:v", "copy",
                "-c:a", "aac",
                "-b:a", "128k",
                "-shortest",
                "-metadata:s:a:0", "language=eng",
                destination
            ],
            "create sample with English audio");
        Assert.True(File.Exists(destination));

        return destination;
    }

    /// <summary>
    /// Alias for <see cref="CreateSampleVideoWithEnglishAudio"/> used by convert tests that need any audio track.
    /// </summary>
    protected string CreateSampleVideoWithSilentAudio(string fileName) =>
        CreateSampleVideoWithEnglishAudio(fileName);

    /// <summary>
    /// Creates a remux of the sample with a silent English audio track and an English SubRip subtitle stream.
    /// </summary>
    protected string CreateSampleVideoWithEnglishSubtitles(string fileName)
    {
        var directory = CreateTempDirectory();
        var destination = Path.Combine(directory, fileName);
        var srtPath = Path.Combine(directory, "sample.eng.srt");

        File.WriteAllText(
            srtPath,
            """
            1
            00:00:00,000 --> 00:00:01,000
            Component test caption.
            """);

        RunFfmpeg(
            [
                "-y",
                "-i", SampleVideoPath,
                "-f", "lavfi",
                "-i", "anullsrc=channel_layout=stereo:sample_rate=48000",
                "-i", srtPath,
                "-c:v", "copy",
                "-c:a", "aac",
                "-b:a", "128k",
                "-c:s", "srt",
                "-shortest",
                "-metadata:s:a:0", "language=eng",
                "-metadata:s:s:0", "language=eng",
                "-map", "0:v:0",
                "-map", "1:a:0",
                "-map", "2:0",
                destination
            ],
            "create sample with English subtitles");
        Assert.True(File.Exists(destination));

        return destination;
    }

    /// <summary>
    /// Creates a remux of the sample with two chapter markers for split tests.
    /// </summary>
    protected string CreateSampleVideoWithChapters(string fileName)
    {
        var directory = CreateTempDirectory();
        var destination = Path.Combine(directory, fileName);
        var metadataPath = Path.Combine(directory, "chapters.ffmeta");

        File.WriteAllText(
            metadataPath,
            """
            ;FFMETADATA1
            [CHAPTER]
            TIMEBASE=1/1000
            START=0
            END=400
            title=Intro
            [CHAPTER]
            TIMEBASE=1/1000
            START=400
            END=1000
            title=Main
            """);

        RunFfmpeg(
            [
                "-y",
                "-i", SampleVideoPath,
                "-i", metadataPath,
                "-map_metadata", "1",
                "-c", "copy",
                destination
            ],
            "create sample with chapters");
        Assert.True(File.Exists(destination));

        return destination;
    }

    private static void RunFfmpeg(IReadOnlyList<string> arguments, string purpose)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        Assert.True(process.Start(), $"Failed to start ffmpeg to {purpose}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(30_000), $"ffmpeg timed out while trying to {purpose}.");
        _ = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        Assert.True(
            process.ExitCode == 0,
            $"ffmpeg failed to {purpose}. Exit code: {process.ExitCode}. stderr: {stderr}");
    }

    protected static PowerShell CreatePowerShellFor<TCmdlet>(string commandName)
    {
        var assembly = typeof(TCmdlet).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(assembly.GetName().FullName!, assembly.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry(commandName, typeof(TCmdlet), null));

        return PowerShell.Create(initialSessionState);
    }

    protected static PowerShell CreatePowerShellWithMediaProvider()
    {
        var assembly = typeof(MediaCmdletProvider).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(assembly.GetName().FullName!, assembly.Location));
        initialSessionState.Providers.Add(
            new SessionStateProviderEntry("Media", typeof(MediaCmdletProvider), null));

        return PowerShell.Create(initialSessionState);
    }

    protected void SkipIfTestAssetsMissing()
    {
        if (File.Exists(SampleVideoPath) && File.Exists(InvalidMediaPath))
            return;

        FailOrSkip("Component test media assets are missing. Generate sample-1s.mkv and invalid-media.mkv under TestAssets.");
    }

    protected static void SkipIfMediaToolsMissing()
    {
        if (IsToolAvailable("ffmpeg") && IsToolAvailable("ffprobe"))
            return;

        FailOrSkip("ffmpeg and/or ffprobe not found on PATH. Install them to run component tests.");
    }

    /// <summary>
    /// Asserts a successful <see cref="MediaConversionResult"/> with path, size, and timing details populated.
    /// </summary>
    /// <param name="requireOutputFileExists">
    /// When false, skips the on-disk output check (e.g. after bonus files are moved into Plex folders).
    /// </param>
    protected static void AssertSuccessfulConversionResult(
        MediaConversionResult result,
        string expectedInputPath,
        string expectedOutputPath,
        bool requireOutputFileExists = true)
    {
        Assert.True(result.Status == MediaConversionResult.CompletedStatus, $"Expected completed Status; got {result.Status}");
        Assert.Equal("Success", result.Status);
        Assert.True(
            string.Equals(result.InputPath, expectedInputPath, StringComparison.OrdinalIgnoreCase),
            $"InputPath mismatch: expected {expectedInputPath}, got {result.InputPath}");
        Assert.True(
            string.Equals(result.OutputPath, expectedOutputPath, StringComparison.OrdinalIgnoreCase),
            $"OutputPath mismatch: expected {expectedOutputPath}, got {result.OutputPath}");
        Assert.Equal(result.InputPath, result.FilePath);
        Assert.True(result.InputSizeBytes > 0, "InputSizeBytes should be > 0");
        Assert.True(result.OutputSizeBytes > 0, "OutputSizeBytes should be > 0");
        Assert.NotNull(result.SizeReductionPercent);
        Assert.True(result.ProcessingTime >= TimeSpan.Zero, "ProcessingTime should be >= 0");

        if (requireOutputFileExists)
            Assert.True(File.Exists(result.OutputPath), $"Expected output file to exist: {result.OutputPath}");
    }

    /// <summary>
    /// When <c>MEDIAFORGE_REQUIRE_COMPONENT_TESTS=1</c> (set in CI), missing tools/assets fail the test
    /// instead of skipping so coverage cannot silently degrade.
    /// </summary>
    private static void FailOrSkip(string message)
    {
        if (RequiresComponentTests)
            throw new InvalidOperationException(message);

        throw SkipException.ForSkip(message);
    }

    private static bool RequiresComponentTests =>
        string.Equals(
            Environment.GetEnvironmentVariable("MEDIAFORGE_REQUIRE_COMPONENT_TESTS"),
            "1",
            StringComparison.Ordinal);

    private static bool IsToolAvailable(string toolName)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = toolName,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
                return false;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return false;
            }

            _ = stdoutTask.GetAwaiter().GetResult();
            _ = stderrTask.GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public virtual void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch
            {
            }
        }
    }
}
