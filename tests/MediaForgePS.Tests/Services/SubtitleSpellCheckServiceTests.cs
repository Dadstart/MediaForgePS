using System;
using System.IO;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitleSpellCheckServiceTests
{
    [Fact]
    public void CheckContent_WithTinyDictionary_ReportsMisspellings()
    {
        var tempDir = CreateTempDictionary(
            dicContent: "2\nhello\nworld\n",
            affContent: "SET UTF-8\n");
        try
        {
            var dicPath = Path.Combine(tempDir, "test.dic");
            var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
            var content = "1\n00:00:01,000 --> 00:00:02,000\nhello misspeled world\n\n";

            var issues = service.CheckContent(
                "sample.srt",
                content,
                new SubtitleSpellCheckOptions(DictionaryPath: dicPath));

            Assert.Single(issues);
            Assert.Equal(["misspeled"], issues[0].Words);
            Assert.Equal("misspeled", issues[0].Word);
            Assert.Equal(1, issues[0].CueNumber);
            Assert.Equal(1, issues[0].LineNumber);
            Assert.Equal("hello misspeled world", issues[0].LineText);
            Assert.Contains("misspeled", issues[0].HighlightedLine);
            Assert.Contains("\u001b[91;1m", issues[0].HighlightedLine);
            Assert.Empty(issues[0].Suggestions);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CheckContent_LeadingHyphen_IsNotPartOfTheWord()
    {
        var tempDir = CreateTempDictionary(
            dicContent: "2\nhello\nworld\n",
            affContent: "SET UTF-8\n");
        try
        {
            var dicPath = Path.Combine(tempDir, "test.dic");
            var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
            var content = "1\n00:00:01,000 --> 00:00:02,000\n-hello misspeled world\n\n";

            var issues = service.CheckContent(
                "sample.srt",
                content,
                new SubtitleSpellCheckOptions(DictionaryPath: dicPath));

            Assert.Single(issues);
            Assert.Equal("misspeled", issues[0].Word);
            Assert.Equal("-hello \u001b[91;1mmisspeled\u001b[0m world", issues[0].HighlightedLine);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CheckContent_IgnoreWord_SuppressesFinding()
    {
        var tempDir = CreateTempDictionary(
            dicContent: "1\nhello\n",
            affContent: "SET UTF-8\n");
        try
        {
            var dicPath = Path.Combine(tempDir, "test.dic");
            var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
            var content = "1\n00:00:01,000 --> 00:00:02,000\nhello Cartman\n\n";

            var issues = service.CheckContent(
                "sample.srt",
                content,
                new SubtitleSpellCheckOptions(
                    IgnoreWords: ["Cartman"],
                    DictionaryPath: dicPath));

            Assert.Empty(issues);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CheckContent_IncludeSuggestions_PopulatesSuggestions()
    {
        var tempDir = CreateTempDictionary(
            dicContent: "2\nhello\nworld\n",
            affContent: "SET UTF-8\n");
        try
        {
            var dicPath = Path.Combine(tempDir, "test.dic");
            var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
            var content = "1\n00:00:01,000 --> 00:00:02,000\nhelo\n\n";

            var issues = service.CheckContent(
                "sample.srt",
                content,
                new SubtitleSpellCheckOptions(
                    DictionaryPath: dicPath,
                    IncludeSuggestions: true));

            Assert.Single(issues);
            Assert.Equal(["helo"], issues[0].Words);
            Assert.Equal("helo", issues[0].Word);
            Assert.NotEmpty(issues[0].Suggestions);
            Assert.Contains("hello", issues[0].Suggestions, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CheckContent_WithEmbeddedDictionary_FlagsUnknownWord()
    {
        var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
        var content = "1\n00:00:01,000 --> 00:00:02,000\nThis is a zxqvword test.\n\n";

        var issues = service.CheckContent("embedded.srt", content);

        Assert.Contains(issues, i => i.Words.Contains("zxqvword", StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, i => i.Words.Contains("This", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void CheckContent_MultipleMisspellingsOnSameLine_EmitsOneIssuePerInstance()
    {
        var tempDir = CreateTempDictionary(
            dicContent: "3\nhello\nand\nworld\n",
            affContent: "SET UTF-8\n");
        try
        {
            var dicPath = Path.Combine(tempDir, "test.dic");
            var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
            var content = "1\n00:00:01,000 --> 00:00:02,000\nhello badword and wrngword\n\n";

            var issues = service.CheckContent(
                "sample.srt",
                content,
                new SubtitleSpellCheckOptions(DictionaryPath: dicPath));

            Assert.Equal(2, issues.Count);
            Assert.Equal("badword", issues[0].Word);
            Assert.Equal("wrngword", issues[1].Word);
            Assert.Equal(1, issues[0].WordOccurrence);
            Assert.Equal(1, issues[1].WordOccurrence);
            Assert.Contains("\u001b[91;1mbadword\u001b[0m", issues[0].HighlightedLine);
            Assert.DoesNotContain("\u001b[91;1mwrngword\u001b[0m", issues[0].HighlightedLine);
            Assert.Contains("\u001b[91;1mwrngword\u001b[0m", issues[1].HighlightedLine);
            Assert.DoesNotContain("\u001b[91;1mbadword\u001b[0m", issues[1].HighlightedLine);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CheckContent_RepeatedWord_EmitsSeparateOccurrences()
    {
        var tempDir = CreateTempDictionary(
            dicContent: "1\nhello\n",
            affContent: "SET UTF-8\n");
        try
        {
            var dicPath = Path.Combine(tempDir, "test.dic");
            var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
            var content = "1\n00:00:01,000 --> 00:00:02,000\nhello badword badword\n\n";

            var issues = service.CheckContent(
                "sample.srt",
                content,
                new SubtitleSpellCheckOptions(DictionaryPath: dicPath));

            Assert.Equal(2, issues.Count);
            Assert.All(issues, i => Assert.Equal("badword", i.Word));
            Assert.Equal(1, issues[0].WordOccurrence);
            Assert.Equal(2, issues[1].WordOccurrence);
            Assert.Equal(
                "hello \u001b[91;1mbadword\u001b[0m badword",
                issues[0].HighlightedLine);
            Assert.Equal(
                "hello badword \u001b[91;1mbadword\u001b[0m",
                issues[1].HighlightedLine);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CheckContent_UppercaseOfRecognizedWord_IsNotAMisspelling()
    {
        var tempDir = CreateTempDictionary(
            dicContent: "4\nhello\nAaron\nMcDonald\niPhone\n",
            affContent: "SET UTF-8\n");
        try
        {
            var dicPath = Path.Combine(tempDir, "test.dic");
            var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
            var options = new SubtitleSpellCheckOptions(DictionaryPath: dicPath);
            var content = "1\n00:00:01,000 --> 00:00:02,000\nHELLO HEllo AaRon McDONALD IPhone aaron Mcdonald Iphone zxqvword\n\n";

            Assert.True(service.IsCorrectlySpelled("HELLO", options));
            Assert.True(service.IsCorrectlySpelled("HEllo", options));
            Assert.True(service.IsCorrectlySpelled("AaRon", options));
            Assert.True(service.IsCorrectlySpelled("McDONALD", options));
            Assert.True(service.IsCorrectlySpelled("IPhone", options));
            Assert.False(service.IsCorrectlySpelled("aaron", options));
            Assert.False(service.IsCorrectlySpelled("Mcdonald", options));
            Assert.False(service.IsCorrectlySpelled("Iphone", options));
            Assert.False(service.IsCorrectlySpelled("zxqvword", options));

            var issues = service.CheckContent("sample.srt", content, options);

            Assert.Equal(["aaron", "Mcdonald", "Iphone", "zxqvword"], issues.Select(issue => issue.Word).ToArray());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CheckContent_EmbeddedDictionary_AllowsUppercaseOfRecognizedWords()
    {
        var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
        var content = "1\n00:00:01,000 --> 00:00:02,000\nHELLO HEllo McDONALD IPhone aaron Nasa Iphone zxqvword\n\n";

        var issues = service.CheckContent("case.srt", content);

        Assert.Equal(["aaron", "Nasa", "Iphone", "zxqvword"], issues.Select(issue => issue.Word).ToArray());
    }

    [Fact]
    public void CheckFile_ReadsSrtFromDisk()
    {
        var tempDir = CreateTempDictionary(
            dicContent: "1\nhello\n",
            affContent: "SET UTF-8\n");
        var srtPath = Path.Combine(tempDir, "clip.srt");
        try
        {
            File.WriteAllText(srtPath, "1\n00:00:01,000 --> 00:00:02,000\nhello badword\n\n");
            var dicPath = Path.Combine(tempDir, "test.dic");
            var service = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);

            var issues = service.CheckFile(
                srtPath,
                new SubtitleSpellCheckOptions(DictionaryPath: dicPath));

            Assert.Single(issues);
            Assert.Equal(["badword"], issues[0].Words);
            Assert.Equal(srtPath, issues[0].Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDictionary(string dicContent, string affContent)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SpellDict_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.dic"), dicContent);
        File.WriteAllText(Path.Combine(tempDir, "test.aff"), affContent);
        return tempDir;
    }
}
