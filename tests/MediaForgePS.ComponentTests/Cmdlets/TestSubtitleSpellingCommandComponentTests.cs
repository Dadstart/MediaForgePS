using System;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class TestSubtitleSpellingCommandComponentTests : ComponentTestBase
{
    public TestSubtitleSpellingCommandComponentTests()
    {
        // Ensure a fresh real DI graph so prior stubbed component tests cannot leave ModuleServices incomplete.
        ModuleServices.ResetForTesting();
    }

    public override void Dispose()
    {
        ModuleServices.ResetForTesting();
        base.Dispose();
    }

    [Fact]
    public void TestSubtitleSpelling_WithMisspelledFixture_ReportsKnownMisspellings()
    {
        SkipIfMisspelledSrtAssetMissing();

        var srtPath = CopyMisspelledSrtAs("spell-check.srt");

        using var ps = CreatePowerShellFor<TestSubtitleSpellingCommand>("Test-SubtitleSpelling");
        ps.AddCommand("Test-SubtitleSpelling")
            .AddParameter("InputPath", srtPath);

        var issues = ps.Invoke().Select(r => r.BaseObject).OfType<SubtitleSpellingIssue>().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(issues, i => i.Word.Equals("misspeled", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, i => i.Word.Equals("Cartman", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, i => i.Word.Equals("Zorpblat", StringComparison.OrdinalIgnoreCase));
        Assert.All(issues, i => Assert.Equal(srtPath, i.Path));
        Assert.DoesNotContain(issues, i => i.Word.Equals("hello", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, i => i.Word.Equals("Everything", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TestSubtitleSpelling_IgnoreWord_SuppressesShowNames()
    {
        SkipIfMisspelledSrtAssetMissing();

        var srtPath = CopyMisspelledSrtAs("spell-ignore.srt");

        using var ps = CreatePowerShellFor<TestSubtitleSpellingCommand>("Test-SubtitleSpelling");
        ps.AddCommand("Test-SubtitleSpelling")
            .AddParameter("InputPath", srtPath)
            .AddParameter("IgnoreWord", new[] { "Cartman", "Zorpblat" });

        var issues = ps.Invoke().Select(r => r.BaseObject).OfType<SubtitleSpellingIssue>().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(issues, i => i.Word.Equals("misspeled", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, i => i.Word.Equals("Cartman", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, i => i.Word.Equals("Zorpblat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TestSubtitleSpelling_WithCleanSrt_ProducesNoOutput()
    {
        var directory = CreateTempDirectory();
        var srtPath = Path.Combine(directory, "clean.srt");
        File.WriteAllText(
            srtPath,
            """
            1
            00:00:01,000 --> 00:00:02,000
            Everything here is spelled correctly.
            """);

        using var ps = CreatePowerShellFor<TestSubtitleSpellingCommand>("Test-SubtitleSpelling");
        ps.AddCommand("Test-SubtitleSpelling")
            .AddParameter("InputPath", srtPath);

        var issues = ps.Invoke().Select(r => r.BaseObject).OfType<SubtitleSpellingIssue>().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Empty(issues);
    }

    [Fact]
    public void TestSubtitleSpelling_IncludeSuggestions_PopulatesSuggestions()
    {
        SkipIfMisspelledSrtAssetMissing();

        var srtPath = CopyMisspelledSrtAs("spell-suggestions.srt");

        using var ps = CreatePowerShellFor<TestSubtitleSpellingCommand>("Test-SubtitleSpelling");
        ps.AddCommand("Test-SubtitleSpelling")
            .AddParameter("InputPath", srtPath)
            .AddParameter("IgnoreWord", new[] { "Cartman", "Zorpblat" })
            .AddParameter("IncludeSuggestions", true);

        var issues = ps.Invoke().Select(r => r.BaseObject).OfType<SubtitleSpellingIssue>().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var misspeled = Assert.Single(issues, i => i.Word.Equals("misspeled", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(misspeled.Suggestions);
        Assert.Contains(misspeled.Suggestions, s => s.Equals("misspelled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TestSubtitleSpelling_WithDirectoryAndRecurse_ChecksNestedSrtFiles()
    {
        SkipIfMisspelledSrtAssetMissing();

        var root = CreateTempDirectory();
        var nested = Path.Combine(root, "season", "ep01");
        Directory.CreateDirectory(nested);
        File.Copy(MisspelledSrtPath, Path.Combine(nested, "nested.srt"));

        using var ps = CreatePowerShellFor<TestSubtitleSpellingCommand>("Test-SubtitleSpelling");
        ps.AddCommand("Test-SubtitleSpelling")
            .AddParameter("InputPath", root)
            .AddParameter("Recurse", true)
            .AddParameter("IgnoreWord", new[] { "Cartman", "Zorpblat" });

        var issues = ps.Invoke().Select(r => r.BaseObject).OfType<SubtitleSpellingIssue>().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Contains(issues, i => i.Word.Equals("misspeled", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, i => i.Path.EndsWith("nested.srt", StringComparison.OrdinalIgnoreCase));
    }

    private void SkipIfMisspelledSrtAssetMissing()
    {
        if (File.Exists(MisspelledSrtPath))
            return;

        FailOrSkip("Component test SRT asset is missing. Add misspelled.srt under TestAssets.");
    }
}
