using System;
using System.Collections.Generic;
using System.Text;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// One misspelled word occurrence in an SRT cue text line.
/// </summary>
/// <param name="Path">Path of the SRT file containing the issue.</param>
/// <param name="CueNumber">Cue sequence number from the SRT (or 1-based index when unparseable).</param>
/// <param name="Timestamp">Cue timestamp line.</param>
/// <param name="LineNumber">1-based line number within the cue text.</param>
/// <param name="Words">Misspelled token for this occurrence (single-item list).</param>
/// <param name="LineText">Original cue text line containing the misspelling.</param>
/// <param name="Suggestions">Suggested corrections when requested; otherwise empty.</param>
/// <param name="WordOccurrence">1-based occurrence of <see cref="Word"/> on the line.</param>
public sealed record SubtitleSpellingIssue(
    string Path,
    int CueNumber,
    string Timestamp,
    int LineNumber,
    IReadOnlyList<string> Words,
    string LineText,
    IReadOnlyList<string> Suggestions,
    int WordOccurrence = 1)
{
    private const string AnsiHighlightPrefix = "\u001b[91;1m";
    private const string AnsiReset = "\u001b[0m";

    /// <summary>
    /// Empty suggestions list for findings produced without suggestion lookup.
    /// </summary>
    public static IReadOnlyList<string> EmptySuggestions { get; } = Array.Empty<string>();

    /// <summary>
    /// Misspelled word for this occurrence.
    /// </summary>
    public string Word =>
        Words.Count == 0 ? string.Empty
        : Words.Count == 1 ? Words[0]
        : string.Join(", ", Words);

    /// <summary>
    /// <see cref="LineText"/> with this occurrence highlighted for console display.
    /// </summary>
    public string HighlightedLine =>
        HighlightWordOccurrence(LineText, Word, Math.Max(0, WordOccurrence - 1));

    /// <summary>
    /// Builds a line with every listed misspelled token highlighted.
    /// </summary>
    public static string HighlightWords(string lineText, IReadOnlyList<string> words)
    {
        if (string.IsNullOrEmpty(lineText) || words is null || words.Count == 0)
            return lineText ?? string.Empty;

        var misspelled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in words)
        {
            if (!string.IsNullOrWhiteSpace(word))
                misspelled.Add(word);
        }

        if (misspelled.Count == 0)
            return lineText;

        return HighlightTokens(lineText, misspelled.Contains);
    }

    /// <summary>
    /// Highlights only the given 0-based occurrence of <paramref name="word"/> on the line.
    /// </summary>
    public static string HighlightWordOccurrence(string lineText, string word, int occurrenceIndex)
    {
        if (string.IsNullOrEmpty(lineText) || string.IsNullOrWhiteSpace(word) || occurrenceIndex < 0)
            return lineText ?? string.Empty;

        var matchIndex = 0;
        return HighlightTokens(
            lineText,
            token =>
            {
                if (!string.Equals(token, word, StringComparison.OrdinalIgnoreCase))
                    return false;

                var shouldHighlight = matchIndex == occurrenceIndex;
                matchIndex++;
                return shouldHighlight;
            });
    }

    /// <summary>
    /// Highlights the exact character span at <paramref name="start"/>/<paramref name="length"/>.
    /// </summary>
    public static string HighlightSpan(string lineText, int start, int length)
    {
        if (string.IsNullOrEmpty(lineText) || start < 0 || length <= 0 || start + length > lineText.Length)
            return lineText ?? string.Empty;

        return lineText[..start]
            + AnsiHighlightPrefix
            + lineText.Substring(start, length)
            + AnsiReset
            + lineText[(start + length)..];
    }

    /// <summary>
    /// Walks word tokens and highlights when <paramref name="shouldHighlight"/> returns true.
    /// </summary>
    private static string HighlightTokens(string lineText, Func<string, bool> shouldHighlight)
    {
        var result = new StringBuilder(lineText.Length + 16);
        var index = 0;
        while (index < lineText.Length)
        {
            if (!IsWordChar(lineText[index]))
            {
                result.Append(lineText[index]);
                index++;
                continue;
            }

            var tokenStart = index;
            index++;
            while (index < lineText.Length && IsWordChar(lineText[index]))
                index++;

            var token = lineText[tokenStart..index];
            if (shouldHighlight(token))
            {
                result.Append(AnsiHighlightPrefix);
                result.Append(token);
                result.Append(AnsiReset);
            }
            else
                result.Append(token);
        }

        return result.ToString();
    }

    private static bool IsWordChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '\'' or '’' or '-';
}
