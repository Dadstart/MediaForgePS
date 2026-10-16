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

    private static readonly ConsoleColor ShortcutColor = ConsoleColor.Cyan;
    private static readonly ConsoleColor CommandColor = ConsoleColor.Gray;
    private static readonly ConsoleColor SuggestionColor = ConsoleColor.Green;
    private static readonly ConsoleColor MisspellingColor = ConsoleColor.Yellow;
    private static readonly ConsoleColor LabelColor = ConsoleColor.DarkGray;

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
            WriteColored(ui, ShortcutColor, "> ");
            var input = (ui.ReadLine() ?? string.Empty).Trim();
            if (input.Length == 0)
                continue;

            if (TryParseSuggestionChoice(input, suggestions.Count, out var suggestionIndex))
                return new SubtitleSpellFixDecision(SubtitleSpellFixAction.Replace, suggestions[suggestionIndex]);

            switch (input.ToLowerInvariant())
            {
                case "c":
                case "custom":
                    {
                        WriteColored(ui, CommandColor, "Replacement for ");
                        WriteColored(ui, MisspellingColor, $"'{prompt.Word}'");
                        WriteColored(ui, CommandColor, ": ");
                        var custom = ui.ReadLine();
                        if (string.IsNullOrWhiteSpace(custom))
                            return new SubtitleSpellFixDecision(SubtitleSpellFixAction.IgnoreOnce);

                        return new SubtitleSpellFixDecision(SubtitleSpellFixAction.Replace, custom.Trim());
                    }

                case "e":
                case "edit":
                    {
                        WriteColored(ui, CommandColor, "Current line: ");
                        WriteHighlightedSpanOnly(ui, prompt);
                        ui.WriteLine();
                        WriteColored(ui, CommandColor, "New line (Enter keeps current): ");
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
                    WriteColored(ui, CommandColor, "Choose a correction for this instance:");
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
        WriteColored(ui, LabelColor, "Cue ");
        WriteColored(ui, CommandColor, prompt.CueNumber.ToString());
        WriteColored(ui, LabelColor, "  ");
        WriteColored(ui, CommandColor, prompt.Timestamp);
        WriteColored(ui, LabelColor, "  (line ");
        WriteColored(ui, CommandColor, prompt.LineNumber.ToString());
        WriteColored(ui, LabelColor, ")");
        ui.WriteLine();

        WriteFullLineWithHighlight(ui, prompt);

        WriteColored(ui, LabelColor, "Misspelling: ");
        WriteColored(ui, MisspellingColor, prompt.Word);
        WriteColored(
            ui,
            LabelColor,
            prompt.InstanceTotal > 0
                ? $"  (instance {prompt.InstanceNumber} of {prompt.InstanceTotal})"
                : $"  (instance {prompt.InstanceNumber})");
        ui.WriteLine();
        WriteColored(ui, CommandColor, "Choose a correction for this instance:");
        ui.WriteLine();
    }

    /// <summary>
    /// Writes the colored action menu (shortcuts vs command labels vs suggestions).
    /// </summary>
    internal static void WriteActionMenu(PSHostUserInterface ui, IReadOnlyList<string> suggestions, string word)
    {
        for (var i = 0; i < suggestions.Count; i++)
        {
            WriteMenuItem(ui, (i + 1).ToString(), suggestions[i], SuggestionColor, $"Replace with '{suggestions[i]}'");
        }

        WriteMenuItem(ui, "C", "Custom", CommandColor, "Type a custom replacement word");
        WriteMenuItem(ui, "E", "Edit line", CommandColor, "Edit the entire subtitle line");
        WriteMenuItem(ui, "I", "Ignore once", CommandColor, "Leave this occurrence unchanged");
        WriteMenuItem(ui, "A", "Ignore all", CommandColor, $"Ignore '{word}' for the rest of this run");
        WriteMenuItem(ui, "F", "Ignore forever", CommandColor, $"Ignore '{word}' and save to the persistent dictionary");
        WriteMenuItem(ui, "S", "Show surrounding", CommandColor, "Show nearby subtitle lines, then choose again");
        WriteMenuItem(ui, "Q", "Quit file", CommandColor, "Stop fixing this file");
    }

    private static void WriteMenuItem(
        PSHostUserInterface ui,
        string shortcut,
        string label,
        ConsoleColor labelColor,
        string description)
    {
        WriteColored(ui, LabelColor, "  [");
        WriteColored(ui, ShortcutColor, shortcut);
        WriteColored(ui, LabelColor, "] ");
        WriteColored(ui, labelColor, label);
        WriteColored(ui, LabelColor, "  - ");
        WriteColored(ui, LabelColor, description);
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
        WriteColored(ui, LabelColor, "Surrounding lines:");
        ui.WriteLine();

        var before = prompt.EffectiveLinesBefore;
        var after = prompt.EffectiveLinesAfter;
        if (before.Count == 0 && after.Count == 0)
        {
            WriteColored(ui, LabelColor, "  (no surrounding lines)");
            ui.WriteLine();
            WriteFullLineWithHighlight(ui, prompt);
            ui.WriteLine();
            return;
        }

        foreach (var line in before)
        {
            WriteColored(ui, LabelColor, "  ");
            WriteColored(ui, CommandColor, line);
            ui.WriteLine();
        }

        WriteColored(ui, ShortcutColor, "> ");
        WriteColored(ui, LabelColor, $"[cue {prompt.CueNumber}] ");
        WriteHighlightedSpanOnly(ui, prompt);
        ui.WriteLine();

        foreach (var line in after)
        {
            WriteColored(ui, LabelColor, "  ");
            WriteColored(ui, CommandColor, line);
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

        WriteColored(ui, LabelColor, "Line: ");
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

        ui.Write(MisspellingColor, background, line.Substring(start, length));

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

    private static void WriteLabelValue(PSHostUserInterface ui, string label, string value)
    {
        WriteColored(ui, LabelColor, label);
        WriteColored(ui, LabelColor, ": ");
        WriteColored(ui, CommandColor, value);
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
