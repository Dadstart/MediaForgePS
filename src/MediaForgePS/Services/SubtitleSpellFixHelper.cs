using System;
using System.Collections.Generic;
using System.Text;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Applies whole-word replacements in SRT cue text and rebuilds SRT documents.
/// </summary>
public static class SubtitleSpellFixHelper
{
    /// <summary>
    /// Finds the next whole-word token at or after <paramref name="searchFrom"/> that is misspelled.
    /// </summary>
    public static bool TryFindNextMisspelling(
        string line,
        int searchFrom,
        HashSet<string> ignoreWords,
        Func<string, bool> isCorrectlySpelled,
        out string word,
        out int start,
        out int length)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(ignoreWords);
        ArgumentNullException.ThrowIfNull(isCorrectlySpelled);

        word = string.Empty;
        start = -1;
        length = 0;

        var index = Math.Clamp(searchFrom, 0, line.Length);
        while (index < line.Length)
        {
            if (!IsWordChar(line[index]))
            {
                index++;
                continue;
            }

            var tokenStart = index;
            index++;
            while (index < line.Length && IsWordChar(line[index]))
                index++;

            var token = line[tokenStart..index];
            if (SubtitleSpellTokenizer.ShouldSkipToken(token))
                continue;

            if (ignoreWords.Contains(token))
                continue;

            if (isCorrectlySpelled(token))
                continue;

            word = token;
            start = tokenStart;
            length = token.Length;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Replaces the span at <paramref name="start"/>/<paramref name="length"/> with a case-adjusted replacement.
    /// </summary>
    public static string ReplaceAt(string line, int start, int length, string originalWord, string replacement)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(originalWord);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (start + length > line.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        var casedReplacement = MatchCasing(originalWord, replacement);
        return string.Concat(line.AsSpan(0, start), casedReplacement, line.AsSpan(start + length));
    }

    /// <summary>
    /// Replaces every whole-word occurrence of <paramref name="word"/> in <paramref name="line"/>
    /// with a case-adjusted <paramref name="replacement"/>.
    /// </summary>
    /// <returns>The updated line and whether any replacement was made.</returns>
    public static (string Line, bool Changed) ReplaceAllWholeWords(string line, string word, string replacement)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(replacement);
        if (word.Length == 0 || line.Length == 0)
            return (line, false);

        var sb = new StringBuilder(line.Length);
        var index = 0;
        var changed = false;
        while (index < line.Length)
        {
            if (!IsWordChar(line[index]))
            {
                sb.Append(line[index]);
                index++;
                continue;
            }

            var tokenStart = index;
            index++;
            while (index < line.Length && IsWordChar(line[index]))
                index++;

            var token = line[tokenStart..index];
            if (string.Equals(token, word, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(MatchCasing(token, replacement));
                changed = true;
            }
            else
            {
                sb.Append(token);
            }
        }

        return changed ? (sb.ToString(), true) : (line, false);
    }

    /// <summary>
    /// Replaces every whole-word occurrence of <paramref name="word"/> across all cue text lines.
    /// </summary>
    /// <returns>True when any line changed.</returns>
    public static bool ReplaceAllWholeWordsInBlocks(
        IReadOnlyList<SrtDocumentHelper.SrtBlock> blocks,
        string word,
        string replacement)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(replacement);

        var changed = false;
        foreach (var block in blocks)
        {
            for (var i = 0; i < block.TextLines.Count; i++)
            {
                var (updated, lineChanged) = ReplaceAllWholeWords(block.TextLines[i], word, replacement);
                if (!lineChanged)
                    continue;

                block.TextLines[i] = updated;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Rebuilds SRT content from cue blocks.
    /// </summary>
    public static string FormatSrt(IReadOnlyList<SrtDocumentHelper.SrtBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            sb.AppendLine(block.SequenceLine);
            sb.AppendLine(block.TimestampLine);
            foreach (var line in block.TextLines)
                sb.AppendLine(line);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds a highlighted line for a single misspelled word (all occurrences).
    /// </summary>
    public static string HighlightWord(string lineText, string word) =>
        SubtitleSpellingIssue.HighlightWords(lineText, [word]);

    /// <summary>
    /// Builds a highlighted line for one exact character span.
    /// </summary>
    public static string HighlightSpan(string lineText, int start, int length) =>
        SubtitleSpellingIssue.HighlightSpan(lineText, start, length);

    /// <summary>
    /// Collects formatted surrounding subtitle text lines around a cue/line position.
    /// </summary>
    /// <param name="blocks">Parsed SRT blocks.</param>
    /// <param name="blockIndex">0-based cue index.</param>
    /// <param name="lineIndex">0-based text line index within the cue.</param>
    /// <param name="radius">Number of neighboring text lines to include on each side.</param>
    public static (IReadOnlyList<string> Before, IReadOnlyList<string> After) GetSurroundingLines(
        IReadOnlyList<SrtDocumentHelper.SrtBlock> blocks,
        int blockIndex,
        int lineIndex,
        int radius = 2)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        var flat = new List<(int BlockIndex, int LineIndex, string Display)>();
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];
            var cueNumber = int.TryParse(block.SequenceLine.Trim(), out var n) && n > 0 ? n : b + 1;
            for (var l = 0; l < block.TextLines.Count; l++)
            {
                flat.Add((
                    b,
                    l,
                    $"[cue {cueNumber}] {block.TextLines[l]}"));
            }
        }

        var currentFlatIndex = -1;
        for (var i = 0; i < flat.Count; i++)
        {
            if (flat[i].BlockIndex == blockIndex && flat[i].LineIndex == lineIndex)
            {
                currentFlatIndex = i;
                break;
            }
        }

        if (currentFlatIndex < 0)
            return (Array.Empty<string>(), Array.Empty<string>());

        var before = new List<string>();
        for (var i = Math.Max(0, currentFlatIndex - radius); i < currentFlatIndex; i++)
            before.Add(flat[i].Display);

        var after = new List<string>();
        for (var i = currentFlatIndex + 1; i <= Math.Min(flat.Count - 1, currentFlatIndex + radius); i++)
            after.Add(flat[i].Display);

        return (before, after);
    }

    /// <summary>
    /// Adjusts <paramref name="replacement"/> casing to resemble <paramref name="original"/>.
    /// </summary>
    public static string MatchCasing(string original, string replacement)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(replacement);
        if (replacement.Length == 0 || original.Length == 0)
            return replacement;

        if (IsAllUpper(original))
            return replacement.ToUpperInvariant();

        if (char.IsUpper(original[0]) && IsAllLower(original.AsSpan(1)))
        {
            if (replacement.Length == 1)
                return replacement.ToUpperInvariant();

            return char.ToUpperInvariant(replacement[0]) + replacement[1..].ToLowerInvariant();
        }

        return replacement;
    }

    private static bool IsAllUpper(string value)
    {
        var hasLetter = false;
        foreach (var ch in value)
        {
            if (!char.IsLetter(ch))
                continue;
            hasLetter = true;
            if (!char.IsUpper(ch))
                return false;
        }

        return hasLetter;
    }

    private static bool IsAllLower(ReadOnlySpan<char> value)
    {
        foreach (var ch in value)
        {
            if (char.IsLetter(ch) && !char.IsLower(ch))
                return false;
        }

        return true;
    }

    private static bool IsWordChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '\'' or '’' or '-';
}
