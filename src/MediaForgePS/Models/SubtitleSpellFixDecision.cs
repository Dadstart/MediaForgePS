namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Action chosen for one interactive misspelling prompt.
/// </summary>
public enum SubtitleSpellFixAction
{
    /// <summary>
    /// Replace the misspelled word with <see cref="SubtitleSpellFixDecision.Replacement"/>.
    /// </summary>
    Replace,

    /// <summary>
    /// Replace every whole-word occurrence of the misspelled word in the current file
    /// with <see cref="SubtitleSpellFixDecision.Replacement"/>.
    /// </summary>
    ReplaceAll,

    /// <summary>
    /// Replace the entire cue text line with <see cref="SubtitleSpellFixDecision.Replacement"/>.
    /// </summary>
    ReplaceLine,

    /// <summary>
    /// Leave this occurrence unchanged and continue.
    /// </summary>
    IgnoreOnce,

    /// <summary>
    /// Leave this occurrence unchanged and ignore the word for the rest of the run.
    /// </summary>
    IgnoreAll,

    /// <summary>
    /// Leave this occurrence unchanged, ignore the word for the rest of the run,
    /// and persist it to the forever-ignore dictionary for future cmdlet calls.
    /// </summary>
    IgnoreForever,

    /// <summary>
    /// Stop interactive fixing for the current file.
    /// </summary>
    Quit
}

/// <summary>
/// Result of prompting the user about one misspelling.
/// </summary>
/// <param name="Action">Chosen action.</param>
/// <param name="Replacement">
/// Replacement word when <see cref="Action"/> is <see cref="SubtitleSpellFixAction.Replace"/>
/// or <see cref="SubtitleSpellFixAction.ReplaceAll"/>,
/// or the full replacement line when <see cref="Action"/> is <see cref="SubtitleSpellFixAction.ReplaceLine"/>.
/// </param>
public sealed record SubtitleSpellFixDecision(
    SubtitleSpellFixAction Action,
    string? Replacement = null);
