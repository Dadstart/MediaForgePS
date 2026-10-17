using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Prompts for misspelling fixes using a colored PowerShell host UI menu.
/// </summary>
public sealed class HostSubtitleSpellFixPrompter : ISubtitleSpellFixPrompter
{
    private const int MaxSuggestionChoices = 5;

    private static readonly ConsoleColor _shortcutColor = ConsoleColor.Cyan;
    private static readonly ConsoleColor _commandColor = ConsoleColor.Gray;
    private static readonly ConsoleColor _suggestionColor = ConsoleColor.Green;
    private static readonly ConsoleColor _misspellingColor = ConsoleColor.Yellow;
    private static readonly ConsoleColor _labelColor = ConsoleColor.DarkGray;

    private readonly PSCmdlet _cmdlet;

    /// <summary>
    /// Creates a host-backed prompter for the given cmdlet.
    /// </summary>
    public HostSubtitleSpellFixPrompter(PSCmdlet cmdlet)
    {
        _cmdlet = cmdlet ?? throw new ArgumentNullException(nameof(cmdlet));
    }

    /// <inheritdoc />
    public SubtitleSpellFixDecision Prompt(SubtitleSpellFixPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var host = _cmdlet.Host;
        var ui = host?.UI;
        if (ui is null)
            throw new InvalidOperationException("An interactive host UI is required for -Fix.");

        WritePromptHeader(ui, prompt);

        var suggestions = TakeSuggestions(prompt.Suggestions);
        while (true)
        {
            WriteActionMenu(ui, suggestions, prompt.Word);
            WriteColored(ui, _shortcutColor, "> ");
            var input = (ui.ReadLine() ?? string.Empty).Trim();
            if (input.Length == 0)
                continue;

            if (TryParseSuggestionChoice(input, suggestions.Count, out var suggestionIndex))
                return new SubtitleSpellFixDecision(SubtitleSpellFixAction.Replace, suggestions[suggestionIndex]);

            if (TryParseReplaceAllSuggestionChoice(input, suggestions.Count, out var replaceAllIndex))
                return CreateReplaceAllDecision(ui, suggestions[replaceAllIndex]);

            switch (input.ToLowerInvariant())
            {
                case "c":
                case "custom":
                    {
                        WriteColored(ui, _commandColor, "Replacement for ");
                        WriteColored(ui, _misspellingColor, $"'{prompt.Word}'");
                        WriteColored(ui, _commandColor, ": ");
                        var custom = ui.ReadLine();
                        if (string.IsNullOrWhiteSpace(custom))
                            return new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreOnce);

                        var replacement = custom.Trim();
                        var scope = PromptReplacementScope(ui, prompt.Word, replacement);
                        return CreateScopedReplacementDecision(replacement, scope);
                    }

                case "r":
                case "replace":
                case "replaceall":
                    {
                        WriteColored(ui, _commandColor, "Replace all ");
                        WriteColored(ui, _misspellingColor, $"'{prompt.Word}'");
                        WriteColored(ui, _commandColor, " with");
                        if (suggestions.Count > 0)
                        {
                            WriteColored(ui, _labelColor, " (suggestion number or typed word)");
                        }

                        WriteColored(ui, _commandColor, ": ");
                        var replaceAllInput = (ui.ReadLine() ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(replaceAllInput))
                            return new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreOnce);

                        if (TryParseSuggestionChoice(replaceAllInput, suggestions.Count, out var allSuggestionIndex))
                            return CreateReplaceAllDecision(ui, suggestions[allSuggestionIndex]);

                        return CreateReplaceAllDecision(ui, replaceAllInput);
                    }

                case "e":
                case "edit":
                    {
                        WriteColored(ui, _commandColor, "Current line: ");
                        WriteHighlightedSpanOnly(ui, prompt);
                        ui.WriteLine();
                        WriteColored(ui, _commandColor, "New line (Enter keeps current): ");
                        var edited = ui.ReadLine();
                        if (edited is null)
                            return new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreOnce);

                        if (edited.Length == 0)
                            return new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreOnce);

                        return new SubtitleSpellFixDecision(SubtitleSpellFixAction.ReplaceLine, edited);
                    }

                case "i":
                case "ignore":
                    return new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreOnce);

                case "a":
                case "all":
                    return new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreAll);

                case "f":
                case "forever":
                    return new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreForever);

                case "s":
                case "surrounding":
                    WriteSurroundingLines(ui, prompt);
                    WriteColored(ui, _commandColor, "Choose a correction for this instance:");
                    ui.WriteLine();
                    continue;

                case "q":
                case "quit":
                    return new SubtitleSpellFixDecision(SubtitleSpellFixAction.Quit);

                default:
                    WriteColored(ui, ConsoleColor.Red, "Unrecognized choice. Enter a number or shortcut from the menu.");
                    ui.WriteLine();
                    continue;
            }
        }
    }

    private static void WritePromptHeader(PSHostUserInterface ui, SubtitleSpellFixPrompt prompt)
    {
        ui.WriteLine(string.Empty);
        WriteLabelValue(ui, "File", prompt.Path);
        WriteColored(ui, _labelColor, "Cue ");
        WriteColored(ui, _commandColor, prompt.CueNumber.ToString());
        WriteColored(ui, _labelColor, "  ");
        WriteColored(ui, _commandColor, prompt.Timestamp);
        WriteColored(ui, _labelColor, "  (line ");
        WriteColored(ui, _commandColor, prompt.LineNumber.ToString());
        WriteColored(ui, _labelColor, ")");
        ui.WriteLine();

        WriteFullLineWithHighlight(ui, prompt);

        WriteColored(ui, _labelColor, "Misspelling: ");
        WriteColored(ui, _misspellingColor, prompt.Word);
        WriteColored(
            ui,
            _labelColor,
            prompt.InstanceTotal > 0
                ? $"  (instance {prompt.InstanceNumber} of {prompt.InstanceTotal})"
                : $"  (instance {prompt.InstanceNumber})");
        ui.WriteLine();
        WriteColored(ui, _commandColor, "Choose a correction for this instance:");
        ui.WriteLine();
    }

    /// <summary>
    /// Writes the colored action menu (shortcuts vs command labels vs suggestions).
    /// </summary>
    internal static void WriteActionMenu(PSHostUserInterface ui, IReadOnlyList<string> suggestions, string word)
    {
        for (var i = 0; i < suggestions.Count; i++)
        {
            WriteMenuItem(ui, (i + 1).ToString(), suggestions[i], _suggestionColor, $"Replace with '{suggestions[i]}'");
        }

        WriteMenuItem(ui, "C", "Custom", _commandColor, "Type a custom replacement, then save it always or forever");
        WriteMenuItem(
            ui,
            "R",
            "Replace all",
            _commandColor,
            $"Replace every '{word}' in this file and optionally save for future runs (R1.. = suggestion)");
        WriteMenuItem(ui, "E", "Edit line", _commandColor, "Edit the entire subtitle line");
        WriteMenuItem(ui, "I", "Ignore once", _commandColor, "Leave this occurrence unchanged");
        WriteMenuItem(ui, "A", "Ignore all", _commandColor, $"Ignore '{word}' for the rest of this run");
        WriteMenuItem(ui, "F", "Ignore forever", _commandColor, $"Ignore '{word}' and save to the persistent dictionary");
        WriteMenuItem(ui, "S", "Show surrounding", _commandColor, "Show nearby subtitle lines, then choose again");
        WriteMenuItem(ui, "Q", "Quit file", _commandColor, "Stop fixing this file");
    }

    private static void WriteMenuItem(
        PSHostUserInterface ui,
        string shortcut,
        string label,
        ConsoleColor labelColor,
        string description)
    {
        WriteColored(ui, _labelColor, "  [");
        WriteColored(ui, _shortcutColor, shortcut);
        WriteColored(ui, _labelColor, "] ");
        WriteColored(ui, labelColor, label);
        WriteColored(ui, _labelColor, "  - ");
        WriteColored(ui, _labelColor, description);
        ui.WriteLine();
    }

    /// <summary>
    /// Writes surrounding cue lines around the current misspelling.
    /// </summary>
    internal static void WriteSurroundingLines(PSHostUserInterface ui, SubtitleSpellFixPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(prompt);

        ui.WriteLine(string.Empty);
        WriteColored(ui, _labelColor, "Surrounding lines:");
        ui.WriteLine();

        var before = prompt.EffectiveLinesBefore;
        var after = prompt.EffectiveLinesAfter;
        if (before.Count == 0 && after.Count == 0)
        {
            WriteColored(ui, _labelColor, "  (no surrounding lines)");
            ui.WriteLine();
            WriteFullLineWithHighlight(ui, prompt);
            ui.WriteLine();
            return;
        }

        foreach (var line in before)
        {
            WriteColored(ui, _labelColor, "  ");
            WriteColored(ui, _commandColor, line);
            ui.WriteLine();
        }

        WriteColored(ui, _shortcutColor, "> ");
        WriteColored(ui, _labelColor, $"[cue {prompt.CueNumber}] ");
        WriteHighlightedSpanOnly(ui, prompt);
        ui.WriteLine();

        foreach (var line in after)
        {
            WriteColored(ui, _labelColor, "  ");
            WriteColored(ui, _commandColor, line);
            ui.WriteLine();
        }

        ui.WriteLine();
    }

    /// <summary>
    /// Writes the full cue line, emphasizing the misspelled span with host colors.
    /// </summary>
    internal static void WriteFullLineWithHighlight(PSHostUserInterface ui, SubtitleSpellFixPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(prompt);

        WriteColored(ui, _labelColor, "Line: ");
        WriteHighlightedSpanOnly(ui, prompt);
        ui.WriteLine();
    }

    /// <summary>
    /// Writes <see cref="SubtitleSpellFixPrompt.LineText"/> with the misspelled span highlighted (no trailing newline).
    /// </summary>
    internal static void WriteHighlightedSpanOnly(PSHostUserInterface ui, SubtitleSpellFixPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(prompt);

        var line = prompt.LineText ?? string.Empty;
        var start = prompt.HighlightStart;
        var length = prompt.HighlightLength;
        var background = ui.RawUI.BackgroundColor;
        var foreground = ui.RawUI.ForegroundColor;

        if (string.IsNullOrEmpty(line) || start < 0 || length <= 0 || start + length > line.Length)
        {
            ui.Write(foreground, background, line);
            return;
        }

        if (start > 0)
            ui.Write(foreground, background, line[..start]);

        ui.Write(_misspellingColor, background, line.Substring(start, length));

        if (start + length < line.Length)
            ui.Write(foreground, background, line[(start + length)..]);
    }

    /// <summary>
    /// Builds a plain-text summary of the current line with the misspelling marked (for tests / fallbacks).
    /// </summary>
    internal static string BuildChoiceMessage(SubtitleSpellFixPrompt prompt)
    {
        var line = prompt.LineText ?? string.Empty;
        var start = prompt.HighlightStart;
        var length = prompt.HighlightLength;
        if (string.IsNullOrEmpty(line) || start < 0 || length <= 0 || start + length > line.Length)
            return $"Line: {line}";

        var marked = line[..start] + ">>>" + line.Substring(start, length) + "<<<" + line[(start + length)..];
        return $"Line: {marked}";
    }

    /// <summary>
    /// Parses a suggestion menu choice such as <c>1</c> or <c>[1]</c>.
    /// </summary>
    internal static bool TryParseSuggestionChoice(string input, int suggestionCount, out int suggestionIndex)
    {
        suggestionIndex = -1;
        if (suggestionCount <= 0 || string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim().Trim('[', ']');
        if (!int.TryParse(trimmed, out var number))
            return false;

        if (number < 1 || number > suggestionCount)
            return false;

        suggestionIndex = number - 1;
        return true;
    }

    /// <summary>
    /// Parses a replace-all suggestion shortcut such as <c>R1</c> or <c>r2</c>.
    /// </summary>
    internal static bool TryParseReplaceAllSuggestionChoice(string input, int suggestionCount, out int suggestionIndex)
    {
        suggestionIndex = -1;
        if (suggestionCount <= 0 || string.IsNullOrWhiteSpace(input) || input.Length < 2)
            return false;

        if (input[0] is not ('R' or 'r'))
            return false;

        return TryParseSuggestionChoice(input[1..], suggestionCount, out suggestionIndex);
    }

    private static SubtitleSpellFixDecision CreateReplaceAllDecision(PSHostUserInterface ui, string replacement)
    {
        var scope = PromptPersistReplacement(ui)
            ? SubtitleSpellReplacementScope.Forever
            : SubtitleSpellReplacementScope.None;
        return new SubtitleSpellFixDecision(SubtitleSpellFixAction.ReplaceAll, replacement, scope);
    }

    private static SubtitleSpellFixDecision CreateScopedReplacementDecision(
        string replacement,
        SubtitleSpellReplacementScope scope)
    {
        if (scope == SubtitleSpellReplacementScope.None)
            return new SubtitleSpellFixDecision(SubtitleSpellFixAction.Replace, replacement);

        return new SubtitleSpellFixDecision(SubtitleSpellFixAction.ReplaceAll, replacement, scope);
    }

    /// <summary>
    /// After a custom replacement, asks whether to remember it for this run or for future runs.
    /// An empty answer keeps the change on this instance only.
    /// </summary>
    internal static SubtitleSpellReplacementScope PromptReplacementScope(
        PSHostUserInterface ui,
        string word,
        string replacement)
    {
        ui.WriteLine();
        WriteColored(ui, _commandColor, "Save ");
        WriteColored(ui, _misspellingColor, $"'{word}'");
        WriteColored(ui, _commandColor, " as ");
        WriteColored(ui, _suggestionColor, $"'{replacement}'");
        WriteColored(ui, _commandColor, "?");
        ui.WriteLine();

        while (true)
        {
            WriteMenuItem(ui, "A", "Always", _commandColor, "Replace every remaining occurrence for the rest of this run");
            WriteMenuItem(ui, "F", "Forever", _commandColor, "Save the replacement for future runs");
            WriteMenuItem(ui, "N", "No", _commandColor, "Change only this instance");
            WriteColored(ui, _shortcutColor, "> ");
            if (TryParseReplacementScope(ui.ReadLine(), out var scope))
                return scope;

            WriteColored(ui, ConsoleColor.Red, "Enter A for always, F for forever, or N for this instance only.");
            ui.WriteLine();
        }
    }

    /// <summary>
    /// Parses an always/forever/no answer. Empty, <c>N</c>, and <c>No</c> mean this instance only.
    /// </summary>
    internal static bool TryParseReplacementScope(string? answer, out SubtitleSpellReplacementScope scope)
    {
        scope = SubtitleSpellReplacementScope.None;
        var trimmed = (answer ?? string.Empty).Trim();
        if (trimmed.Length == 0 ||
            trimmed.Equals("n", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("no", StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed.Equals("a", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("always", StringComparison.OrdinalIgnoreCase))
        {
            scope = SubtitleSpellReplacementScope.Always;
            return true;
        }

        if (trimmed.Equals("f", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("forever", StringComparison.OrdinalIgnoreCase))
        {
            scope = SubtitleSpellReplacementScope.Forever;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Asks whether to remember a Replace All mapping for future runs. Empty answer defaults to yes.
    /// </summary>
    internal static bool PromptPersistReplacement(PSHostUserInterface ui)
    {
        WriteColored(ui, _commandColor, "Save this replacement for future runs? [");
        WriteColored(ui, _shortcutColor, "Y");
        WriteColored(ui, _commandColor, "/n]: ");
        var answer = (ui.ReadLine() ?? string.Empty).Trim();
        if (answer.Length == 0)
            return true;

        return answer.StartsWith('y') || answer.StartsWith('Y');
    }

    private static void WriteLabelValue(PSHostUserInterface ui, string label, string value)
    {
        WriteColored(ui, _labelColor, label);
        WriteColored(ui, _labelColor, ": ");
        WriteColored(ui, _commandColor, value);
        ui.WriteLine();
    }

    private static void WriteColored(PSHostUserInterface ui, ConsoleColor foreground, string text)
    {
        ui.Write(foreground, ui.RawUI.BackgroundColor, text);
    }

    private static IReadOnlyList<string> TakeSuggestions(IReadOnlyList<string> suggestions)
    {
        if (suggestions is null || suggestions.Count == 0)
            return Array.Empty<string>();

        if (suggestions.Count <= MaxSuggestionChoices)
            return suggestions;

        var limited = new string[MaxSuggestionChoices];
        for (var i = 0; i < MaxSuggestionChoices; i++)
            limited[i] = suggestions[i];
        return limited;
    }
}
