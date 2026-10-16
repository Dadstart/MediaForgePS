using System;
using System.Collections.Generic;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// One misspelled word occurrence found while spell-checking an SRT file.
/// </summary>
/// <param name="Path">Path of the SRT file containing the issue.</param>
/// <param name="CueNumber">Cue sequence number from the SRT (or 1-based index when unparseable).</param>
/// <param name="Timestamp">Cue timestamp line.</param>
/// <param name="LineNumber">1-based line number within the cue text.</param>
/// <param name="Word">Misspelled token as it appeared after tag stripping.</param>
/// <param name="LineText">Original cue text line containing the word.</param>
/// <param name="Suggestions">Suggested corrections when requested; otherwise empty.</param>
public sealed record SubtitleSpellingIssue(
    string Path,
    int CueNumber,
    string Timestamp,
    int LineNumber,
    string Word,
    string LineText,
    IReadOnlyList<string> Suggestions)
{
    /// <summary>
    /// Empty suggestions list for findings produced without suggestion lookup.
    /// </summary>
    public static IReadOnlyList<string> EmptySuggestions { get; } = Array.Empty<string>();
}
