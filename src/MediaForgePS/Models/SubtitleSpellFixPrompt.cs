using System;
using System.Collections.Generic;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Context shown when prompting to fix one misspelled word occurrence.
/// </summary>
/// <param name="Path">SRT file path.</param>
/// <param name="CueNumber">Cue sequence number.</param>
/// <param name="Timestamp">Cue timestamp line.</param>
/// <param name="LineNumber">1-based line number within the cue text.</param>
/// <param name="Word">Misspelled token.</param>
/// <param name="LineText">Full cue text line containing the misspelling.</param>
/// <param name="HighlightStart">0-based start index of this occurrence in <see cref="LineText"/>.</param>
/// <param name="HighlightLength">Length of this occurrence in <see cref="LineText"/>.</param>
/// <param name="HighlightedLine">Cue line with this occurrence highlighted (ANSI), for non-host consumers.</param>
/// <param name="Suggestions">Suggested replacements.</param>
/// <param name="LinesBefore">Formatted surrounding lines before the current line.</param>
/// <param name="LinesAfter">Formatted surrounding lines after the current line.</param>
/// <param name="InstanceNumber">1-based instance number for this word in the current file so far.</param>
/// <param name="InstanceTotal">Total remaining+current instances of this word known at prompt time (optional; 0 when unknown).</param>
public sealed record SubtitleSpellFixPrompt(
    string Path,
    int CueNumber,
    string Timestamp,
    int LineNumber,
    string Word,
    string LineText,
    int HighlightStart,
    int HighlightLength,
    string HighlightedLine,
    IReadOnlyList<string> Suggestions,
    IReadOnlyList<string>? LinesBefore = null,
    IReadOnlyList<string>? LinesAfter = null,
    int InstanceNumber = 1,
    int InstanceTotal = 0)
{
    /// <summary>
    /// Surrounding lines before the current line.
    /// </summary>
    public IReadOnlyList<string> EffectiveLinesBefore => LinesBefore ?? Array.Empty<string>();

    /// <summary>
    /// Surrounding lines after the current line.
    /// </summary>
    public IReadOnlyList<string> EffectiveLinesAfter => LinesAfter ?? Array.Empty<string>();
}
