using System;
using System.Collections.Generic;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Parses SubRip (SRT) documents into cue blocks.
/// </summary>
public static class SrtDocumentHelper
{
    /// <summary>
    /// Parses SRT content into sequence/timestamp/text blocks. Blank-line separated cues only.
    /// </summary>
    /// <param name="content">Raw SRT content (any line endings).</param>
    /// <returns>Parsed cue blocks; malformed short parts are skipped.</returns>
    public static List<SrtBlock> ParseSrtBlocks(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        var blocks = new List<SrtBlock>();
        var parts = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var lines = part.Split('\n');
            if (lines.Length < 3)
                continue;

            var textLines = new List<string>(lines.Length - 2);
            for (var i = 2; i < lines.Length; i++)
                textLines.Add(lines[i]);

            blocks.Add(new SrtBlock(lines[0], lines[1], textLines));
        }

        return blocks;
    }

    /// <summary>
    /// One SRT cue: sequence line, timestamp line, and text lines.
    /// </summary>
    /// <param name="SequenceLine">Cue index line (typically a number).</param>
    /// <param name="TimestampLine">Cue timing line.</param>
    /// <param name="TextLines">Subtitle text lines for the cue.</param>
    public sealed record SrtBlock(string SequenceLine, string TimestampLine, List<string> TextLines);
}
