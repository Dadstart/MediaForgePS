using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Spell-checks SRT subtitle text using a Hunspell dictionary.
/// </summary>
public interface ISubtitleSpellCheckService
{
    /// <summary>
    /// Reads an SRT file and returns misspelling findings.
    /// </summary>
    /// <param name="path">Path to the SRT file.</param>
    /// <param name="options">Spell-check options; null uses defaults.</param>
    IReadOnlyList<SubtitleSpellingIssue> CheckFile(string path, SubtitleSpellCheckOptions? options = null);

    /// <summary>
    /// Spell-checks SRT content already loaded in memory.
    /// </summary>
    /// <param name="path">Display path associated with the content.</param>
    /// <param name="content">Raw SRT content.</param>
    /// <param name="options">Spell-check options; null uses defaults.</param>
    IReadOnlyList<SubtitleSpellingIssue> CheckContent(
        string path,
        string content,
        SubtitleSpellCheckOptions? options = null);

    /// <summary>
    /// Returns whether <paramref name="word"/> is correctly spelled for the configured dictionary.
    /// A recognized spelling may gain capitals without being treated as a misspelling.
    /// </summary>
    bool IsCorrectlySpelled(string word, SubtitleSpellCheckOptions? options = null);

    /// <summary>
    /// Returns spelling suggestions for <paramref name="word"/>.
    /// </summary>
    IReadOnlyList<string> GetSuggestions(string word, SubtitleSpellCheckOptions? options = null);
}
