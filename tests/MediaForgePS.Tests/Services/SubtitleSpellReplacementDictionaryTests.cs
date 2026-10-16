using System;
using System.IO;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitleSpellReplacementDictionaryTests
{
    [Fact]
    public void LoadReplacements_WhenFileMissing_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_Replace_" + Guid.NewGuid().ToString("N") + ".txt");
        var dictionary = new SubtitleSpellReplacementDictionary(
            NullLogger<SubtitleSpellReplacementDictionary>.Instance,
            path);

        Assert.Empty(dictionary.LoadReplacements());
    }

    [Fact]
    public void AddReplacement_PersistsAndLoadsAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_Replace_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var first = new SubtitleSpellReplacementDictionary(
                NullLogger<SubtitleSpellReplacementDictionary>.Instance,
                path);
            Assert.True(first.AddReplacement("misspeled", "misspelled"));
            Assert.False(first.AddReplacement("Misspeled", "misspelled"));

            var second = new SubtitleSpellReplacementDictionary(
                NullLogger<SubtitleSpellReplacementDictionary>.Instance,
                path);
            var map = second.LoadReplacements();

            Assert.True(map.TryGetValue("misspeled", out var replacement));
            Assert.Equal("misspelled", replacement);
            Assert.Contains("misspeled=misspelled", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void AddReplacement_UpdatesExistingTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_Replace_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var dictionary = new SubtitleSpellReplacementDictionary(
                NullLogger<SubtitleSpellReplacementDictionary>.Instance,
                path);
            Assert.True(dictionary.AddReplacement("teh", "the"));
            Assert.True(dictionary.AddReplacement("teh", "thy"));

            Assert.Equal("thy", dictionary.LoadReplacements()["teh"]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LoadReplacements_SkipsCommentsBlankAndInvalidLines()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_Replace_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(path, "# comment\n\nmisspeled=misspelled\nbadline\n=onlyto\nfrom=\n");
            var dictionary = new SubtitleSpellReplacementDictionary(
                NullLogger<SubtitleSpellReplacementDictionary>.Instance,
                path);

            var map = dictionary.LoadReplacements();
            Assert.Single(map);
            Assert.Equal("misspelled", map["misspeled"]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
