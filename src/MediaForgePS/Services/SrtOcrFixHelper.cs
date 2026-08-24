using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Fixes common OCR errors in SRT subtitle text (music note ♪ misreads, pipe as I, brackets, etc.).
/// </summary>
public static partial class SrtOcrFixHelper
{
    private const char MusicNote = '♪';

    [GeneratedRegex(@"(^|\s|<i>|</i>)[J3S&¢d$g](\s|$|<i>|</i>)")]
    private static partial Regex MusicNoteMisreadRegex();

    [GeneratedRegex(@"(^|\s|<i>|</i>)\|(\s|$|<i>|</i>)")]
    private static partial Regex SolitaryPipeRegex();

    [GeneratedRegex(@"([a-zA-Z])\|([a-zA-Z])")]
    private static partial Regex MidWordPipeRegex();

    [GeneratedRegex(@"(down )10 (South)")]
    private static partial Regex Down10SouthRegex();

    [GeneratedRegex(@"(\s|</i>|<i>)I$")]
    private static partial Regex TrailingIMusicNoteRegex();

    /// <summary>
    /// Reads an SRT file, applies OCR fixes, and writes the result to outputPath atomically. Uses UTF-8 encoding.
    /// </summary>
    /// <param name="inputPath">Path to the source SRT file.</param>
    /// <param name="outputPath">Path to write the repaired SRT file.</param>
    public static void RepairFile(string inputPath, string outputPath)
    {
        var content = File.ReadAllText(inputPath).Replace("\r\n", "\n").Replace("\r", "\n");
        var fixedContent = FixMusicNoteOcrErrors(content);
        AtomicFileHelper.WriteTextAtomically(outputPath, fixedContent, Encoding.UTF8, overwrite: true);
    }

    /// <summary>
    /// Parses SRT content and fixes OCR misreads of ♪ (often detected as J, 3, S, or trailing I) in subtitle text only.
    /// </summary>
    /// <param name="content">Raw SRT content (any line endings).</param>
    /// <returns>Fixed SRT content with normalized line endings preserved in structure.</returns>
    public static string FixMusicNoteOcrErrors(string content)
    {
        var blocks = ParseSrtBlocks(content);
        var sb = new StringBuilder();

        foreach (var block in blocks)
        {
            sb.AppendLine(block.SequenceLine);
            sb.AppendLine(block.TimestampLine);
            foreach (var line in block.TextLines)
                sb.AppendLine(FixMusicNoteInLine(line));
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    internal static List<SrtBlock> ParseSrtBlocks(string content)
    {
        var blocks = new List<SrtBlock>();
        var parts = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var lines = part.Split('\n');
            if (lines.Length < 3)
                continue;

            var sequenceLine = lines[0];
            var timestampLine = lines[1];
            var textLines = new List<string>();
            for (var i = 2; i < lines.Length; i++)
                textLines.Add(lines[i]);

            blocks.Add(new SrtBlock(sequenceLine, timestampLine, textLines));
        }

        return blocks;
    }

    /// <summary>
    /// Replaces OCR misreads in a single subtitle text line: &lt;i&gt;33&lt;/i&gt; → &lt;i&gt;♪♪&lt;/i&gt;; [$10]/[$20] → [♪♪♪]; ♪ (J, 3, S, &, trailing I) → ♪; solitary | → I.
    /// Boundaries include whitespace and &lt;i&gt; / &lt;/i&gt; tags.
    /// </summary>
    internal static string FixMusicNoteInLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        var s = line;

        // Replace <i>33</i> (OCR misread of two music notes) with <i>♪♪</i>.
        s = s.Replace("<i>33</i>", $"<i>{MusicNote}{MusicNote}</i>");

        // Replace [$10] / [$20] (OCR misread of three music notes) with [♪♪♪].
        var threeNotes = $"[{MusicNote}{MusicNote}{MusicNote}]";
        s = s.Replace("[$10]", threeNotes).Replace("[$20]", threeNotes);

        // Replace J, 3, S, &, ¢, d, $ or g with ♪ when they appear in "music note" positions (standalone or at boundaries).
        s = MusicNoteMisreadRegex().Replace(s, $"$1{MusicNote}$2");

        // Replace solitary '|' with 'I' (OCR often misreads capital I as pipe).
        s = SolitaryPipeRegex().Replace(s, "$1I$2");

        // Replace '|' with 'I' when it appears in the middle of a word (letter | letter).
        s = MidWordPipeRegex().Replace(s, "$1I$2");

        // Replace any remaining '|' with 'I' (OCR misread).
        s = s.Replace('|', 'I');

        // Replace standalone unmatched brackets '[' and ']' with 'I' (OCR misread).
        s = ReplaceUnmatchedBrackets(s);

        // Fix OCR misread in South Park lyric: "down 10 South" → "down to South".
        s = Down10SouthRegex().Replace(s, "$1to $2");

        // Replace trailing I with ♪ when it looks like an OCR'd music note at end of line.
        s = TrailingIMusicNoteRegex().Replace(s, $"$1{MusicNote}");
        if (s.Length == 1 && s[0] == 'I')
            s = MusicNote.ToString();

        return s;
    }

    private static string ReplaceUnmatchedBrackets(string s)
    {
        var chars = s.ToCharArray();
        var unmatchedOpen = new Stack<int>();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] == '[')
                unmatchedOpen.Push(i);
            else if (chars[i] == ']')
            {
                if (unmatchedOpen.Count > 0)
                    unmatchedOpen.Pop();
                else
                    chars[i] = 'I';
            }
        }

        foreach (var i in unmatchedOpen)
            chars[i] = 'I';

        return new string(chars);
    }

    internal record SrtBlock(string SequenceLine, string TimestampLine, List<string> TextLines);
}
