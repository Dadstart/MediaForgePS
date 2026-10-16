using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Prompts for a manual decision on one misspelled subtitle word.
/// </summary>
public interface ISubtitleSpellFixPrompter
{
    /// <summary>
    /// Asks how to handle the misspelling described by <paramref name="prompt"/>.
    /// </summary>
    SubtitleSpellFixDecision Prompt(SubtitleSpellFixPrompt prompt);
}
