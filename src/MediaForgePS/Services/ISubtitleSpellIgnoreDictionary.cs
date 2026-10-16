using System.Collections.Generic;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Persistent word list of forever-ignored subtitle spellings.
/// </summary>
public interface ISubtitleSpellIgnoreDictionary
{
    /// <summary>
    /// Path to the on-disk ignore dictionary file.
    /// </summary>
    string DictionaryPath { get; }

    /// <summary>
    /// Loads ignored words from disk (empty when the file does not exist).
    /// </summary>
    IReadOnlyCollection<string> LoadWords();

    /// <summary>
    /// Adds <paramref name="word"/> to the in-memory set and appends it to disk when new.
    /// </summary>
    /// <returns>True when the word was newly added; false when it was already present.</returns>
    bool AddWord(string word);
}
