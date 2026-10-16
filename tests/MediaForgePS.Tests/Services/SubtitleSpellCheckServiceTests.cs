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
            Assert.Equal("misspeled", issues[0].Word);
            Assert.Equal(1, issues[0].CueNumber);
            Assert.Equal(1, issues[0].LineNumber);
            Assert.Equal("hello misspeled world", issues[0].LineText);
            Assert.Empty(issues[0].Suggestions);
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

        Assert.Contains(issues, i => i.Word.Equals("zxqvword", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, i => i.Word.Equals("This", StringComparison.OrdinalIgnoreCase));
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
            Assert.Equal("badword", issues[0].Word);
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
