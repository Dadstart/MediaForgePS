using System;
using System.Collections.Generic;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Options for an SRT spell-check run.
/// </summary>
/// <param name="IgnoreWords">Words to treat as correctly spelled for this run.</param>
/// <param name="DictionaryPath">Optional path to a Hunspell <c>.dic</c> file; companion <c>.aff</c> must share the same stem.</param>
/// <param name="IncludeSuggestions">When true, populate suggestion lists (more expensive).</param>
public sealed record SubtitleSpellCheckOptions(
    IReadOnlyList<string>? IgnoreWords = null,
    string? DictionaryPath = null,
    bool IncludeSuggestions = false)
{
    /// <summary>
    /// Default options: embedded en_US dictionary, no ignore list, no suggestions.
    /// </summary>
    public static SubtitleSpellCheckOptions Default { get; } = new();
}
