using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
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

    protected static PowerShell CreatePowerShellFor<TCmdlet>(string commandName)
    {
        var assembly = typeof(TCmdlet).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(assembly.GetName().FullName!, assembly.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry(commandName, typeof(TCmdlet), null));

        return PowerShell.Create(initialSessionState);
    }

    protected void SkipIfTestAssetsMissing()
    {
        if (!File.Exists(SampleVideoPath) || !File.Exists(InvalidMediaPath))
            throw SkipException.ForSkip("Component test media assets are missing. Generate sample-1s.mkv and invalid-media.mkv under TestAssets.");
    }

    protected static void SkipIfMediaToolsMissing()
    {
        if (!IsToolAvailable("ffmpeg") || !IsToolAvailable("ffprobe"))
            throw SkipException.ForSkip("ffmpeg and/or ffprobe not found on PATH. Install them to run component tests.");
    }

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
