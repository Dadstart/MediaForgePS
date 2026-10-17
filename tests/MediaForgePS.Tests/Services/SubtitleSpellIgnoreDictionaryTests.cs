using System;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitleSpellIgnoreDictionaryTests
{
    [Fact]
    public void LoadWords_WhenFileMissing_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_Ignore_" + Guid.NewGuid().ToString("N") + ".txt");
        var dictionary = new SubtitleSpellIgnoreDictionary(
            NullLogger<SubtitleSpellIgnoreDictionary>.Instance,
            path);

        Assert.Empty(dictionary.LoadWords());
    }

    [Fact]
    public void AddWord_PersistsAndLoadsAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_Ignore_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var first = new SubtitleSpellIgnoreDictionary(
                NullLogger<SubtitleSpellIgnoreDictionary>.Instance,
                path);
            Assert.True(first.AddWord("Cartman"));
            Assert.False(first.AddWord("cartman"));

            var second = new SubtitleSpellIgnoreDictionary(
                NullLogger<SubtitleSpellIgnoreDictionary>.Instance,
                path);
            var words = second.LoadWords();

            Assert.Contains("Cartman", words, StringComparer.OrdinalIgnoreCase);
            Assert.True(File.Exists(path));
            Assert.Contains("Cartman", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LoadWords_SkipsCommentsAndBlankLines()
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaForgePS_Ignore_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(path, "# comment\n\nKenny\n\n");
            var dictionary = new SubtitleSpellIgnoreDictionary(
                NullLogger<SubtitleSpellIgnoreDictionary>.Instance,
                path);

            Assert.Equal(["Kenny"], dictionary.LoadWords().ToArray());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
