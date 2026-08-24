using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dadstart.Labs.MediaForge.Services.BonusProcessing;

internal static class BonusPlexLayout
{
    internal static readonly (string FolderName, string Suffix)[] _entries =
    {
        ("Behind The Scenes", "behindthescenes"),
        ("Deleted Scenes", "deleted"),
        ("Featurettes", "featurette"),
        ("Interviews", "interview"),
        ("Scenes", "scene"),
        ("Shorts", "short"),
        ("Trailers", "trailer"),
        ("Other", "other")
    };

    internal static readonly string[] _subtitleExtensions = { "srt", "vtt" };

    internal static IReadOnlyList<string> GetBonusSuffixes() =>
        _entries.Select(entry => entry.Suffix).ToList();

    internal static IReadOnlyList<string> GetBonusMkvPaths(string inputDirectory)
    {
        var bonusSuffixes = GetBonusSuffixes();
        return Directory.EnumerateFiles(inputDirectory, "*.mkv", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var baseName = Path.GetFileNameWithoutExtension(path);
                return bonusSuffixes.Any(suffix =>
                    baseName.EndsWith($"-{suffix}", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
    }
}
