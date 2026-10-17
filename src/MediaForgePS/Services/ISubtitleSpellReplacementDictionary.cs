using System.Collections.Generic;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Persistent misspelling-to-correction map remembered from Replace All decisions.
/// </summary>
public interface ISubtitleSpellReplacementDictionary
{
    /// <summary>
    /// Path to the on-disk replacement dictionary file.
    /// </summary>
    string DictionaryPath { get; }

    /// <summary>
    /// Loads misspelling → replacement pairs from disk (empty when the file does not exist).
    /// </summary>
    IReadOnlyDictionary<string, string> LoadReplacements();

    /// <summary>
    /// Adds or updates a replacement mapping and writes it to disk.
    /// </summary>
    /// <returns>
    /// True when the mapping was newly added or the replacement text changed;
    /// false when the same mapping was already present.
    /// </returns>
    bool AddReplacement(string misspelling, string replacement);
}
