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
        Assert.True(result.ProcessingTime > TimeSpan.Zero, "ProcessingTime should be > 0");

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

            if (!process.WaitForExit(5000))
                return false;

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
