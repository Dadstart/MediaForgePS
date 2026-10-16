using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Runs an interactive spelling-fix pass over one SRT file.
/// </summary>
public sealed class SubtitleSpellFixSession
{
    private readonly ISubtitleSpellCheckService _spellCheckService;
    private readonly ISubtitleSpellFixPrompter _prompter;
    private readonly ISubtitleSpellIgnoreDictionary? _ignoreDictionary;

    /// <summary>
    /// Creates a fix session.
    /// </summary>
    public SubtitleSpellFixSession(
        ISubtitleSpellCheckService spellCheckService,
        ISubtitleSpellFixPrompter prompter,
        ISubtitleSpellIgnoreDictionary? ignoreDictionary = null)
    {
        _spellCheckService = spellCheckService ?? throw new ArgumentNullException(nameof(spellCheckService));
        _prompter = prompter ?? throw new ArgumentNullException(nameof(prompter));
        _ignoreDictionary = ignoreDictionary;
    }

    /// <summary>
    /// Interactively fixes misspellings in <paramref name="path"/> and writes the file when changed.
    /// </summary>
    /// <returns>True when the file content changed and was written.</returns>
    public bool FixFile(
        string path,
        SubtitleSpellCheckOptions options,
        HashSet<string> sessionIgnoreWords)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sessionIgnoreWords);

        var originalContent = File.ReadAllText(path);
        var content = originalContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        var blocks = SrtDocumentHelper.ParseSrtBlocks(content);
        var changed = false;
        var quit = false;
        var promptedInstanceByWord = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var blockIndex = 0; blockIndex < blocks.Count && !quit; blockIndex++)
        {
            var block = blocks[blockIndex];
            var cueNumber = ResolveCueNumber(block.SequenceLine, blockIndex);
            for (var lineIndex = 0; lineIndex < block.TextLines.Count && !quit; lineIndex++)
            {
                var line = block.TextLines[lineIndex];
                var searchFrom = 0;
                var guard = 0;
                while (guard++ < 10_000 &&
                       SubtitleSpellFixHelper.TryFindNextMisspelling(
                           line,
                           searchFrom,
                           sessionIgnoreWords,
                           word => _spellCheckService.IsCorrectlySpelled(word, options),
                           out var word,
                           out var start,
                           out var length))
                {
                    promptedInstanceByWord.TryGetValue(word, out var priorInstances);
                    var instanceNumber = priorInstances + 1;
                    promptedInstanceByWord[word] = instanceNumber;

                    var suggestions = _spellCheckService.GetSuggestions(word, options);
                    var (linesBefore, linesAfter) = SubtitleSpellFixHelper.GetSurroundingLines(
                        blocks,
                        blockIndex,
                        lineIndex);
                    var prompt = new SubtitleSpellFixPrompt(
                        path,
                        cueNumber,
                        block.TimestampLine,
                        lineIndex + 1,
                        word,
                        line,
                        start,
                        length,
                        SubtitleSpellFixHelper.HighlightSpan(line, start, length),
                        suggestions,
                        linesBefore,
                        linesAfter,
                        instanceNumber);

                    var decision = _prompter.Prompt(prompt);
                    switch (decision.Action)
                    {
                        case SubtitleSpellFixAction.Replace:
                            if (string.IsNullOrWhiteSpace(decision.Replacement))
                            {
                                searchFrom = start + length;
                                break;
                            }

                            line = SubtitleSpellFixHelper.ReplaceAt(
                                line,
                                start,
                                length,
                                word,
                                decision.Replacement);
                            block.TextLines[lineIndex] = line;
                            changed = true;
                            searchFrom = start + SubtitleSpellFixHelper.MatchCasing(word, decision.Replacement).Length;
                            break;

                        case SubtitleSpellFixAction.ReplaceAll:
                            if (string.IsNullOrWhiteSpace(decision.Replacement))
                            {
                                searchFrom = start + length;
                                break;
                            }

                            var originalToken = line.Substring(start, length);
                            var (prefixAfterReplace, _) = SubtitleSpellFixHelper.ReplaceAllWholeWords(
                                line[..start],
                                word,
                                decision.Replacement);
                            if (SubtitleSpellFixHelper.ReplaceAllWholeWordsInBlocks(
                                    blocks,
                                    word,
                                    decision.Replacement))
                                changed = true;

                            line = block.TextLines[lineIndex];
                            // Map past this occurrence on the rewritten line (earlier same-word hits may shift indexes).
                            searchFrom = prefixAfterReplace.Length
                                + SubtitleSpellFixHelper.MatchCasing(originalToken, decision.Replacement).Length;
                            break;

                        case SubtitleSpellFixAction.ReplaceLine:
                            if (decision.Replacement is null)
                            {
                                searchFrom = start + length;
                                break;
                            }

                            line = decision.Replacement;
                            block.TextLines[lineIndex] = line;
                            changed = true;
                            // Re-scan the edited line from the start for any remaining misspellings.
                            searchFrom = 0;
                            break;

                        case SubtitleSpellFixAction.IgnoreOnce:
                            searchFrom = start + length;
                            break;

                        case SubtitleSpellFixAction.IgnoreAll:
                            sessionIgnoreWords.Add(word);
                            searchFrom = start + length;
                            break;

                        case SubtitleSpellFixAction.IgnoreForever:
                            sessionIgnoreWords.Add(word);
                            _ignoreDictionary?.AddWord(word);
                            searchFrom = start + length;
                            break;

                        case SubtitleSpellFixAction.Quit:
                            quit = true;
                            break;

                        default:
                            searchFrom = start + length;
                            break;
                    }

                    if (quit)
                        break;
                }
            }
        }

        if (!changed)
            return false;

        var fixedContent = SubtitleSpellFixHelper.FormatSrt(blocks);
        AtomicFileHelper.WriteTextAtomically(path, fixedContent, Encoding.UTF8, overwrite: true);
        return true;
    }

    private static int ResolveCueNumber(string sequenceLine, int blockIndex)
    {
        if (int.TryParse(sequenceLine.Trim(), out var cueNumber) && cueNumber > 0)
            return cueNumber;

        return blockIndex + 1;
    }
}
