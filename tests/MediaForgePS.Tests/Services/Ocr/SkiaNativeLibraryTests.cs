using System.Diagnostics;
using Dadstart.Labs.MediaForge.Services.Ocr;
using SkiaSharp;
using Xunit;
using Xunit.Sdk;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ocr;

public class SkiaNativeLibraryTests : IDisposable
{
    private readonly string _tempDir;

    public SkiaNativeLibraryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SkiaNative_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ResolveLibraryPath_WhenRidFolderPresent_ReturnsThatPath()
    {
        var expected = WriteNativePlaceholder("win-x64", "libSkiaSharp.dll");

        var resolved = SkiaNativeLibrary.ResolveLibraryPath(_tempDir, "win-x64", "libSkiaSharp.dll");

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void ResolveLibraryPath_WhenOnlyRuntimesFolderPresent_ReturnsThatPath()
    {
        var expected = Path.Combine(_tempDir, "runtimes", "win-x64", "native", "libSkiaSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
        File.WriteAllBytes(expected, [1]);

        var resolved = SkiaNativeLibrary.ResolveLibraryPath(_tempDir, "win-x64", "libSkiaSharp.dll");

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void ResolveLibraryPath_WhenMissing_ReturnsNull()
    {
        var resolved = SkiaNativeLibrary.ResolveLibraryPath(_tempDir, "win-x64", "libSkiaSharp.dll");

        Assert.Null(resolved);
    }

    [Fact]
    public void EnsureLoadedFrom_WhenLibraryMissing_ThrowsDllNotFound()
    {
        var ex = Assert.Throws<DllNotFoundException>(() => SkiaNativeLibrary.EnsureLoadedFrom(_tempDir));

        Assert.Contains("libSkiaSharp", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleOutput_ContainsLibSkiaSharpForWindowsRids()
    {
        var moduleOutput = FindModuleOutputDirectory();
        var x64 = Path.Combine(moduleOutput, "win-x64", "libSkiaSharp.dll");

        Assert.True(File.Exists(x64), $"libSkiaSharp.dll was not copied next to the module at '{x64}'.");
        Assert.True(new FileInfo(x64).Length > 0, $"libSkiaSharp.dll at '{x64}' is empty.");
    }

    [Fact]
    public async Task EnsureLoaded_FromPowerShellModuleDirectory_CreatesBitmap()
    {
        if (!OperatingSystem.IsWindows())
            throw SkipException.ForSkip("libSkiaSharp in the module is a Windows binary.");

        var moduleOutput = FindModuleOutputDirectory();
        var manifest = Path.Combine(moduleOutput, "MediaForgePS.psd1");
        var moduleAssembly = Path.Combine(moduleOutput, "MediaForgePS.dll");
        var skiaAssembly = Path.Combine(moduleOutput, "SkiaSharp.dll");
        Assert.True(File.Exists(manifest), $"Module manifest was not found at '{manifest}'.");
        Assert.True(File.Exists(moduleAssembly), $"Module assembly was not found at '{moduleAssembly}'.");
        Assert.True(File.Exists(skiaAssembly), $"SkiaSharp.dll was not found at '{skiaAssembly}'.");

        var scriptPath = Path.Combine(_tempDir, "load-skia.ps1");
        File.WriteAllText(scriptPath, $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module -Name '{{manifest}}' -Force
            $assembly = [System.Reflection.Assembly]::LoadFrom('{{moduleAssembly}}')
            $type = $assembly.GetType('Dadstart.Labs.MediaForge.Services.Ocr.SkiaNativeLibrary', $true)
            $flags = [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::NonPublic
            $method = $type.GetMethod('EnsureLoaded', $flags)
            if ($null -eq $method) { throw 'EnsureLoaded was not found.' }
            $method.Invoke($null, $null)
            [System.Reflection.Assembly]::LoadFrom('{{skiaAssembly}}') | Out-Null
            $bitmap = [SkiaSharp.SKBitmap]::new(4, 4)
            if ($bitmap.Width -ne 4) { throw "Unexpected bitmap width $($bitmap.Width)." }
            $bitmap.Dispose()
            [GC]::Collect()
            [GC]::WaitForPendingFinalizers()
            Write-Output 'skia-ok'
            """);

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
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(scriptPath);

        Assert.True(process.Start(), "Failed to start pwsh.");
        var cancellationToken = TestContext.Current.CancellationToken;
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(60), cancellationToken)).ConfigureAwait(true);
        Assert.True(completed == waitTask, "pwsh timed out while loading libSkiaSharp from the module directory.");
        await waitTask.ConfigureAwait(true);
        var stdout = await stdoutTask.ConfigureAwait(true);
        var stderr = await stderrTask.ConfigureAwait(true);

        Assert.True(
            process.ExitCode == 0,
            $"pwsh failed to load libSkiaSharp from the module. Exit code: {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
        Assert.Contains("skia-ok", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureLoaded_InProcess_CreatesBitmap()
    {
        if (!OperatingSystem.IsWindows())
            throw SkipException.ForSkip("libSkiaSharp in the module is a Windows binary.");

        SkiaNativeLibrary.EnsureLoaded();
        using var bitmap = new SKBitmap(4, 4);

        Assert.Equal(4, bitmap.Width);
        Assert.Equal(4, bitmap.Height);
    }

    private string WriteNativePlaceholder(string runtimeIdentifier, string fileName)
    {
        var path = Path.Combine(_tempDir, runtimeIdentifier, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1]);
        return path;
    }

    private static string FindModuleOutputDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string? configuration = null;
        while (directory is not null)
        {
            if (directory.Parent is not null &&
                directory.Parent.Name.Equals("bin", StringComparison.OrdinalIgnoreCase))
                configuration = directory.Name;

            if (File.Exists(Path.Combine(directory.FullName, "MediaForgePS.sln")))
            {
                if (configuration is null)
                    throw new InvalidOperationException($"Could not determine the build configuration from '{AppContext.BaseDirectory}'.");

                return Path.Combine(directory.FullName, "src", "MediaForgePS", "bin", configuration, "net10.0");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate the repository root from '{AppContext.BaseDirectory}'.");
    }
}
