using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Xml.Linq;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Providers;

public class MediaFormatViewsTests
{
    [Fact]
    public void FormatFile_DefinesViewsForCoreMediaTypes()
    {
        var formatPath = FindFormatFile();
        var document = XDocument.Load(formatPath);
        var typeNames = document
            .Descendants("TypeName")
            .Select(e => e.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(typeof(MediaFile).FullName!, typeNames);
        Assert.Contains(typeof(MediaFormat).FullName!, typeNames);
        Assert.Contains(typeof(MediaStream).FullName!, typeNames);
        Assert.Contains(typeof(MediaChapter).FullName!, typeNames);
        Assert.Contains(typeof(CopyAudioTrackMapping).FullName!, typeNames);
        Assert.Contains(typeof(EncodeAudioTrackMapping).FullName!, typeNames);
        Assert.Contains(typeof(ConstantRateVideoEncodingSettings).FullName!, typeNames);
        Assert.Contains(typeof(VariableRateVideoEncodingSettings).FullName!, typeNames);
        Assert.Contains(typeof(NvencVideoEncodingSettings).FullName!, typeNames);
    }

    [Fact]
    public void FormatFile_LoadsWithUpdateFormatData()
    {
        var formatPath = FindFormatFile();
        var initialSessionState = InitialSessionState.CreateDefault();
        // Format ps1xml ScriptBlocks are subject to execution policy; bypass so CI/local Restricted hosts can load views.
        initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

        using var ps = PowerShell.Create(initialSessionState);
        ps.AddCommand("Update-FormatData").AddParameter("AppendPath", formatPath);
        ps.Invoke();
        Assert.Empty(ps.Streams.Error);
    }

    private static string FindFormatFile()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(MediaFile).Assembly.Location)!;
        var candidates = new[]
        {
            Path.Combine(assemblyDir, "Formats", "MediaForgePS.format.ps1xml"),
            Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "..", "src", "MediaForgePS", "Formats", "MediaForgePS.format.ps1xml")),
        };

        var existing = candidates.FirstOrDefault(File.Exists);
        Assert.True(existing is not null, "MediaForgePS.format.ps1xml was not found next to the assembly or in source.");
        return existing!;
    }
}
