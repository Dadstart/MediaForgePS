using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Xunit;
using Xunit.Sdk;

namespace Dadstart.Labs.MediaForge.E2ETests;

public abstract class E2ETestBase : IDisposable
{
    private static readonly object _packLock = new();
    private static string? _cachedModuleManifest;
    private static string? _cachedConfiguration;

    private readonly List<string> _tempDirectories = new();
    private PowerShell? _powerShell;

    protected string AssetsRoot { get; }

    protected string SampleVideoPath =>
        Path.Combine(AssetsRoot, "sample-1s.mkv");

    protected E2ETestBase()
    {
        AssetsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets");
    }

    protected string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS.E2ETests");
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }

    protected PowerShell ImportPackedModule()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var repoRoot = FindRepoRoot();
        var configuration = ResolveBuildConfiguration(repoRoot);
        var moduleManifest = EnsurePackedModule(repoRoot, configuration);

        var initialSessionState = InitialSessionState.CreateDefault();
        // Format .ps1xml load is gated by execution policy (Restricted on Windows CI runners).
        // ExecutionPolicy is not supported on Unix/macOS and throws PlatformNotSupportedException if set.
        if (OperatingSystem.IsWindows())
            initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

        _powerShell = PowerShell.Create(initialSessionState);
        _powerShell.AddCommand("Import-Module")
            .AddParameter("Name", moduleManifest)
            .AddParameter("Force", true);
        _powerShell.Invoke();
        Assert.Empty(_powerShell.Streams.Error.ReadAll());
        _powerShell.Commands.Clear();

        return _powerShell;
    }

    private static string EnsurePackedModule(string repoRoot, string configuration)
    {
        lock (_packLock)
        {
            if (_cachedModuleManifest is not null &&
                string.Equals(_cachedConfiguration, configuration, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(_cachedModuleManifest))
                return _cachedModuleManifest;

            var packScript = Path.Combine(repoRoot, "scripts", "Pack-Module.ps1");
            Assert.True(File.Exists(packScript), $"Pack script not found: {packScript}");

            RunPwsh(
                new[]
                {
                    "-NoProfile",
                    "-File",
                    packScript,
                    "-Configuration",
                    configuration,
                    "-RepoRoot",
                    repoRoot
                },
                "pack MediaForgePS module");

            var moduleManifest = Path.Combine(repoRoot, "artifacts", "MediaForgePS", "MediaForgePS.psd1");
            Assert.True(File.Exists(moduleManifest), $"Packed module manifest not found: {moduleManifest}");

            _cachedModuleManifest = moduleManifest;
            _cachedConfiguration = configuration;
            return moduleManifest;
        }
    }

    protected void SkipIfTestAssetsMissing()
    {
        if (File.Exists(SampleVideoPath))
            return;

        FailOrSkip("E2E test media assets are missing. Ensure sample-1s.mkv is under TestAssets.");
    }

    protected static void SkipIfMediaToolsMissing()
    {
        if (IsToolAvailable("ffmpeg") && IsToolAvailable("ffprobe"))
            return;

        FailOrSkip("ffmpeg and/or ffprobe not found on PATH. Install them to run E2E tests.");
    }

    private static string ResolveBuildConfiguration(string repoRoot)
    {
        var preferred = Environment.GetEnvironmentVariable("MEDIAFORGE_CONFIGURATION");
        if (!string.IsNullOrWhiteSpace(preferred) &&
            HasBuiltModule(repoRoot, preferred))
            return preferred;

        if (HasBuiltModule(repoRoot, "Release"))
            return "Release";

        if (HasBuiltModule(repoRoot, "Debug"))
            return "Debug";

        FailOrSkip("No built MediaForgePS.dll found under bin/Release or bin/Debug. Build the solution first.");
        return "Debug";
    }

    private static bool HasBuiltModule(string repoRoot, string configuration)
    {
        var output = Path.Combine(repoRoot, "src", "MediaForgePS", "bin", configuration, "net10.0");
        var dll = Path.Combine(output, "MediaForgePS.dll");
        var manifest = Path.Combine(output, "MediaForgePS.psd1");
        var formats = Path.Combine(output, "Formats");
        return File.Exists(dll) && File.Exists(manifest) && Directory.Exists(formats);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var packScript = Path.Combine(directory.FullName, "scripts", "Pack-Module.ps1");
            var sln = Path.Combine(directory.FullName, "MediaForgePS.sln");
            if (File.Exists(packScript) && File.Exists(sln))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from E2E test base directory.");
    }

    private static void RunPwsh(IReadOnlyList<string> arguments, string purpose)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        Assert.True(process.Start(), $"Failed to start pwsh to {purpose}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), $"pwsh timed out while trying to {purpose}.");
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        Assert.True(
            process.ExitCode == 0,
            $"pwsh failed to {purpose}. Exit code: {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
    }

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
        if (_powerShell is not null)
        {
            try
            {
                _powerShell.Commands.Clear();
                _powerShell.AddCommand("Remove-Module")
                    .AddParameter("Name", "MediaForgePS")
                    .AddParameter("Force", true)
                    .AddParameter("ErrorAction", ActionPreference.SilentlyContinue);
                _powerShell.Invoke();
            }
            catch
            {
            }

            _powerShell.Dispose();
            _powerShell = null;
        }

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
