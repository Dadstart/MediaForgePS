using System;
using System.Collections.Generic;
using System.IO;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitleSpellFixSessionTests
{
    [Fact]
    public void FixFile_AppliesReplacementsFromPrompter()
    {
        var tempDir = CreateTempDictionary("2\nhello\nworld\n", "SET UTF-8\n");
        var srtPath = Path.Combine(tempDir, "clip.srt");
        try
        {
            File.WriteAllText(srtPath, "1\n00:00:01,000 --> 00:00:02,000\nhello misspeled world\n\n");
            var dicPath = Path.Combine(tempDir, "test.dic");
            var spell = CreateSpellServiceWithoutSuggestions();
            var prompter = new ScriptedPrompter([
                new SubtitleSpellFixDecision(SubtitleSpellFixAction.Replace, "misspelled")
            ]);
            var session = new SubtitleSpellFixSession(spell, prompter);
            var options = new SubtitleSpellCheckOptions(DictionaryPath: dicPath);
            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var wrote = session.FixFile(srtPath, options, ignore);

            Assert.True(wrote);
            Assert.Contains("hello misspelled world", File.ReadAllText(srtPath));
            Assert.Single(prompter.Prompts);
            Assert.Equal("misspeled", prompter.Prompts[0].Word);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FixFile_IgnoreAll_SkipsLaterOccurrences()
    {
        var tempDir = CreateTempDictionary("1\nhello\n", "SET UTF-8\n");
        var srtPath = Path.Combine(tempDir, "clip.srt");
        try
        {
            File.WriteAllText(
                srtPath,
                """
                1
                00:00:01,000 --> 00:00:02,000
                hello Cartman

                2
                00:00:02,000 --> 00:00:03,000
                hello Cartman
                """);
            var dicPath = Path.Combine(tempDir, "test.dic");
            var spell = CreateSpellServiceWithoutSuggestions();
            var prompter = new ScriptedPrompter([
                new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreAll)
            ]);
            var session = new SubtitleSpellFixSession(spell, prompter);
            var options = new SubtitleSpellCheckOptions(DictionaryPath: dicPath);
            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var wrote = session.FixFile(srtPath, options, ignore);

            Assert.False(wrote);
            Assert.Contains("Cartman", ignore, StringComparer.OrdinalIgnoreCase);
            Assert.Single(prompter.Prompts);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FixFile_IgnoreForever_PersistsWordAndSkipsLaterOccurrences()
    {
        var tempDir = CreateTempDictionary("1\nhello\n", "SET UTF-8\n");
        var srtPath = Path.Combine(tempDir, "clip.srt");
        var ignorePath = Path.Combine(tempDir, "spelling-ignore.txt");
        try
        {
            File.WriteAllText(
                srtPath,
                """
                1
                00:00:01,000 --> 00:00:02,000
                hello Cartman

                2
                00:00:02,000 --> 00:00:03,000
                hello Cartman
                """);
            var dicPath = Path.Combine(tempDir, "test.dic");
            var spell = CreateSpellServiceWithoutSuggestions();
            var ignoreDictionary = new SubtitleSpellIgnoreDictionary(
                NullLogger<SubtitleSpellIgnoreDictionary>.Instance,
                ignorePath);
            var prompter = new ScriptedPrompter([
                new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreForever)
            ]);
            var session = new SubtitleSpellFixSession(spell, prompter, ignoreDictionary);
            var options = new SubtitleSpellCheckOptions(DictionaryPath: dicPath);
            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var wrote = session.FixFile(srtPath, options, ignore);

            Assert.False(wrote);
            Assert.Contains("Cartman", ignore, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Cartman", ignoreDictionary.LoadWords(), StringComparer.OrdinalIgnoreCase);
            Assert.True(File.Exists(ignorePath));
            Assert.Single(prompter.Prompts);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FixFile_ReplaceAll_ReplacesEveryOccurrenceInFile()
    {
        var tempDir = CreateTempDictionary("4\nhello\nworld\nmisspelled\nagain\n", "SET UTF-8\n");
        var srtPath = Path.Combine(tempDir, "clip.srt");
        try
        {
            File.WriteAllText(
                srtPath,
                """
                1
                00:00:01,000 --> 00:00:02,000
                hello misspeled world

                2
                00:00:02,000 --> 00:00:03,000
                misspeled again
                """);
            var dicPath = Path.Combine(tempDir, "test.dic");
            var spell = CreateSpellServiceWithoutSuggestions();
            var prompter = new ScriptedPrompter([
                new SubtitleSpellFixDecision(SubtitleSpellFixAction.ReplaceAll, "misspelled")
            ]);
            var session = new SubtitleSpellFixSession(spell, prompter);
            var options = new SubtitleSpellCheckOptions(DictionaryPath: dicPath);
            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var wrote = session.FixFile(srtPath, options, ignore);

            Assert.True(wrote);
            var content = File.ReadAllText(srtPath);
            Assert.Contains("hello misspelled world", content);
            Assert.Contains("misspelled again", content);
            Assert.DoesNotContain("misspeled", content);
            Assert.Single(prompter.Prompts);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FixFile_ReplaceLine_ReplacesEntireLineAndRescans()
    {
        var tempDir = CreateTempDictionary("2\nhello\nworld\n", "SET UTF-8\n");
        var srtPath = Path.Combine(tempDir, "clip.srt");
        try
        {
            File.WriteAllText(srtPath, "1\n00:00:01,000 --> 00:00:02,000\nhello misspeled world\n\n");
            var dicPath = Path.Combine(tempDir, "test.dic");
            var spell = CreateSpellServiceWithoutSuggestions();
            var prompter = new ScriptedPrompter([
                new SubtitleSpellFixDecision(SubtitleSpellFixAction.ReplaceLine, "hello world")
            ]);
            var session = new SubtitleSpellFixSession(spell, prompter);
            var options = new SubtitleSpellCheckOptions(DictionaryPath: dicPath);
            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var wrote = session.FixFile(srtPath, options, ignore);

            Assert.True(wrote);
            Assert.Contains("hello world", File.ReadAllText(srtPath));
            Assert.DoesNotContain("misspeled", File.ReadAllText(srtPath));
            Assert.Single(prompter.Prompts);
            Assert.Equal("misspeled", prompter.Prompts[0].Word);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void FixFile_Quit_StopsWithoutWritingFurtherChanges()
    {
        var tempDir = CreateTempDictionary("1\nhello\n", "SET UTF-8\n");
        var srtPath = Path.Combine(tempDir, "clip.srt");
        try
        {
            File.WriteAllText(
                srtPath,
                """
                1
                00:00:01,000 --> 00:00:02,000
                hello badword

                2
                00:00:02,000 --> 00:00:03,000
                hello wrngword
                """);
            var dicPath = Path.Combine(tempDir, "test.dic");
            var spell = CreateSpellServiceWithoutSuggestions();
            var prompter = new ScriptedPrompter([
                new SubtitleSpellFixDecision(SubtitleSpellFixAction.Quit)
            ]);
            var session = new SubtitleSpellFixSession(spell, prompter);
            var options = new SubtitleSpellCheckOptions(DictionaryPath: dicPath);
            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var original = File.ReadAllText(srtPath);

            var wrote = session.FixFile(srtPath, options, ignore);

            Assert.False(wrote);
            Assert.Equal(original, File.ReadAllText(srtPath));
            Assert.Single(prompter.Prompts);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static ISubtitleSpellCheckService CreateSpellServiceWithoutSuggestions()
    {
        var inner = new SubtitleSpellCheckService(NullLogger<SubtitleSpellCheckService>.Instance);
        return new SuggestionFreeSpellCheckService(inner);
    }

    private static string CreateTempDictionary(string dicContent, string affContent)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SpellFix_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.dic"), dicContent);
        File.WriteAllText(Path.Combine(tempDir, "test.aff"), affContent);
        return tempDir;
    }

    /// <summary>
    /// Tiny Hunspell fixtures can hang in Suggest; interactive fix tests only need Check.
    /// </summary>
    private sealed class SuggestionFreeSpellCheckService(ISubtitleSpellCheckService inner) : ISubtitleSpellCheckService
    {
        public IReadOnlyList<SubtitleSpellingIssue> CheckFile(string path, SubtitleSpellCheckOptions? options = null) =>
            inner.CheckFile(path, options);

        public IReadOnlyList<SubtitleSpellingIssue> CheckContent(
            string path,
            string content,
            SubtitleSpellCheckOptions? options = null) =>
            inner.CheckContent(path, content, options);

        public bool IsCorrectlySpelled(string word, SubtitleSpellCheckOptions? options = null) =>
            inner.IsCorrectlySpelled(word, options);

        public IReadOnlyList<string> GetSuggestions(string word, SubtitleSpellCheckOptions? options = null) =>
            Array.Empty<string>();
    }

    private sealed class ScriptedPrompter : ISubtitleSpellFixPrompter
    {
        private readonly Queue<SubtitleSpellFixDecision> _decisions;

        public ScriptedPrompter(IEnumerable<SubtitleSpellFixDecision> decisions)
        {
            _decisions = new Queue<SubtitleSpellFixDecision>(decisions);
        }

        public List<SubtitleSpellFixPrompt> Prompts { get; } = new();

        public SubtitleSpellFixDecision Prompt(SubtitleSpellFixPrompt prompt)
        {
            Prompts.Add(prompt);
            return _decisions.Count > 0
                ? _decisions.Dequeue()
                : new SubtitleSpellFixDecision(SubtitleSpellFixAction.Quit);
        }
    }
}
