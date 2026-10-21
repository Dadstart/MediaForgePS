using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Tokenizes SRT cue text for spell checking.
/// </summary>
public static partial class SubtitleSpellTokenizer
{
    private const char MusicNote = '♪';
    private const int MaxAcronymLength = 4;

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\{[^}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex AssOverrideRegex();

    /// <summary>
    /// Strips HTML-like and ASS override tags from a subtitle line.
    /// </summary>
    public static string StripFormatting(string line)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;

        var withoutHtml = HtmlTagRegex().Replace(line, string.Empty);
        return AssOverrideRegex().Replace(withoutHtml, string.Empty);
    }

    /// <summary>
    /// Extracts spell-check candidate words from a cue text line.
    /// A hyphen at the beginning of the line is ignored.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string line)
    {
        var stripped = StripFormatting(line);
        if (string.IsNullOrWhiteSpace(stripped))
            return Array.Empty<string>();

        var tokens = new List<string>();
        var current = new StringBuilder();
        var index = 0;
        while (index < stripped.Length)
        {
            if (IsTokenBreak(stripped, index))
            {
                TryAddToken(tokens, current);
                index++;
                continue;
            }

            current.Append(stripped[index]);
            index++;
        }

        TryAddToken(tokens, current);
        return tokens;
    }

    /// <summary>
    /// True when <paramref name="index"/> is a dialogue hyphen at the start of <paramref name="line"/>.
    /// Only whitespace and other hyphens may appear before that index.
    /// </summary>
    internal static bool IsLeadingDialogueHyphen(string line, int index)
    {
        if (string.IsNullOrEmpty(line) || index < 0 || index >= line.Length || line[index] != '-')
            return false;

        for (var i = 0; i < index; i++)
        {
            var ch = line[i];
            if (ch != '-' && !char.IsWhiteSpace(ch))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true when a token should be skipped instead of spell-checked.
    /// </summary>
    public static bool ShouldSkipToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return true;

        if (token.Length == 1 && !char.IsLetter(token[0]))
            return true;

        if (IsMusicNoteOnly(token))
            return true;

        if (IsPureNumber(token))
            return true;

        if (IsShortAcronym(token))
            return true;

        return !ContainsLetter(token);
    }

    private static bool IsTokenBreak(string line, int index) =>
        IsLeadingDialogueHyphen(line, index) || !IsWordChar(line[index]);

    private static bool IsWordChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '\'' or '’' or '-';

    private static void TryAddToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
            return;

        var token = TrimTokenEdges(current.ToString());
        current.Clear();
        if (!string.IsNullOrEmpty(token) && !ShouldSkipToken(token))
            tokens.Add(token);
    }

    private static string TrimTokenEdges(string token)
    {
        var start = 0;
        var end = token.Length - 1;
        while (start <= end && IsEdgePunctuation(token[start]))
            start++;
        while (end >= start && IsEdgePunctuation(token[end]))
            end--;

        return start > end ? string.Empty : token[start..(end + 1)];
    }

    private static bool IsEdgePunctuation(char ch) =>
        ch is '\'' or '’' or '-';

    private static bool IsMusicNoteOnly(string token)
    {
        foreach (var ch in token)
        {
            if (ch != MusicNote)
                return false;
        }

        return token.Length > 0;
    }

    private static bool IsPureNumber(string token)
    {
        foreach (var ch in token)
        {
            if (!char.IsDigit(ch))
                return false;
        }

        return token.Length > 0;
    }

    private static bool IsShortAcronym(string token) =>
        token.Length <= MaxAcronymLength && IsAllUpperLetters(token);

    private static bool IsAllUpperLetters(string token)
    {
        foreach (var ch in token)
        {
            if (!char.IsLetter(ch) || !char.IsUpper(ch))
                return false;
        }

        return token.Length > 0;
    }

    private static bool ContainsLetter(string token)
    {
        foreach (var ch in token)
        {
            if (char.IsLetter(ch))
                return true;
        }

        return false;
    }
}
