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
        Assert.Contains(typeof(MediaConversionResult).FullName!, typeNames);
        Assert.Contains(typeof(MediaConversionStatistics).FullName!, typeNames);
        Assert.Contains(typeof(SubtitleProcessingResult).FullName!, typeNames);
    }

    [Fact]
    public void FormatFile_LoadsWithUpdateFormatData()
    {
        var formatPath = FindFormatFile();
        using var ps = CreatePowerShellWithFormatData(formatPath);
        Assert.Empty(ps.Streams.Error);
    }

    [Fact]
    public void SubtitleProcessingResult_FormatView_ShowsFileNamesOnly()
    {
        var formatPath = FindFormatFile();
        using var ps = CreatePowerShellWithFormatData(formatPath);

        var result = SubtitleProcessingResult.Create(
            [@"C:\media\bonus\title.eng.srt", @"C:\media\bonus\title.eng.sup"],
            [@"C:\media\bonus\title.eng.ocr.srt"]);

        var table = FormatObject(ps, result);
        Assert.Contains("title.eng.srt", table, StringComparison.Ordinal);
        Assert.Contains("title.eng.sup", table, StringComparison.Ordinal);
        Assert.Contains("title.eng.ocr.srt", table, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\media", table, StringComparison.Ordinal);

        var list = FormatObject(ps, result, useFormatList: true);
        Assert.Contains("title.eng.srt", list, StringComparison.Ordinal);
        Assert.Contains("title.eng.sup", list, StringComparison.Ordinal);
        Assert.Contains("title.eng.ocr.srt", list, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\media", list, StringComparison.Ordinal);
    }

    [Fact]
    public void MediaConversionResult_FormatView_ShowsOutputFileNameOnly()
    {
        var formatPath = FindFormatFile();
        using var ps = CreatePowerShellWithFormatData(formatPath);

        var result = new MediaConversionResult(
            @"C:\media\input.mkv",
            @"C:\media\output\episode.mp4",
            MediaConversionResult.CompletedStatus,
            2.0,
            1.0,
            50.0,
            TimeSpan.FromSeconds(12));

        var rendered = FormatObject(ps, result);

        Assert.Contains("episode.mp4", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\media", rendered, StringComparison.Ordinal);
    }

    private static string FormatObject(PowerShell ps, object value, bool useFormatList = false)
    {
        ps.Commands.Clear();
        if (useFormatList)
        {
            ps.AddCommand("Format-List").AddParameter("InputObject", value);
            ps.AddCommand("Out-String");
        }
        else
            ps.AddCommand("Out-String").AddParameter("InputObject", value);

        var rendered = string.Join(Environment.NewLine, ps.Invoke().Select(r => r.BaseObject?.ToString()));
        Assert.Empty(ps.Streams.Error);
        return rendered;
    }

    private static PowerShell CreatePowerShellWithFormatData(string formatPath)
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        // Format ps1xml ScriptBlocks are subject to execution policy on Windows; bypass so Restricted hosts can load views.
        // ExecutionPolicy is not supported on Unix/macOS and throws PlatformNotSupportedException if set.
        if (OperatingSystem.IsWindows())
            initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

        var ps = PowerShell.Create(initialSessionState);
        ps.AddCommand("Update-FormatData").AddParameter("AppendPath", formatPath);
        ps.Invoke();
        ps.Commands.Clear();
        return ps;
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
