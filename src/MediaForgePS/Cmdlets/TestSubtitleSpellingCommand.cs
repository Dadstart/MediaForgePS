using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Checks SRT subtitle files for misspellings and optionally prompts to fix each one.
/// </summary>
/// <remarks>
/// Without <c>-Fix</c>, files with no issues produce no pipeline output.
/// With <c>-Fix</c>, prompts interactively for each misspelling and writes corrected files in place.
/// Remaining unfixed issues are written to the pipeline.
/// Supports <c>-WhatIf</c> and <c>-Confirm</c> when fixing.
/// A hyphen at the beginning of a cue text line is a dialogue marker and is not part of the first word.
/// </remarks>
[Cmdlet(VerbsDiagnostic.Test, "SubtitleSpelling", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(SubtitleSpellingIssue))]
public class TestSubtitleSpellingCommand : CmdletBase
{
    /// <summary>
    /// Path to an SRT file or directory containing .srt files. Pipeline accepts path strings.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Path to SRT file(s) or directory containing .srt files.")]
    [Alias("Path")]
    [ValidateNotNullOrEmpty]
    public string[] InputPath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// When input is a directory, recurse into subdirectories to find .srt files.
    /// </summary>
    [Parameter(HelpMessage = "When input is a directory, recurse into subdirectories.")]
    public SwitchParameter Recurse { get; set; }

    /// <summary>
    /// Words to treat as correctly spelled for this run (names, show-specific terms).
    /// </summary>
    [Parameter(HelpMessage = "Words to ignore during spell checking.")]
    public string[]? IgnoreWord { get; set; }

    /// <summary>
    /// Optional path to a Hunspell .dic file. Companion .aff must share the same stem.
    /// When omitted, the embedded en_US dictionary is used.
    /// </summary>
    [Parameter(HelpMessage = "Path to a Hunspell .dic file (companion .aff required).")]
    public string? DictionaryPath { get; set; }

    /// <summary>
    /// When set, each reported issue includes suggested corrections.
    /// Implied when <see cref="Fix"/> is set.
    /// </summary>
    [Parameter(HelpMessage = "Include spelling suggestions (slower). Implied by -Fix.")]
    public SwitchParameter IncludeSuggestions { get; set; }

    /// <summary>
    /// Interactively prompt to fix each misspelling and write corrected SRT files in place.
    /// </summary>
    [Parameter(HelpMessage = "Prompt to fix each misspelling interactively and write files in place.")]
    public SwitchParameter Fix { get; set; }

    /// <summary>
    /// Optional path to a persistent forever-ignore word list (one word per line).
    /// Defaults to <c>%LocalApplicationData%/MediaForgePS/spelling-ignore.txt</c>.
    /// </summary>
    [Parameter(HelpMessage = "Path to the persistent forever-ignore dictionary file.")]
    public string? IgnoreDictionaryPath { get; set; }

    /// <summary>
    /// Optional path to a persistent Replace All mapping file (<c>misspelling=replacement</c> per line).
    /// Defaults to <c>%LocalApplicationData%/MediaForgePS/spelling-replacements.txt</c>.
    /// Applied automatically when <see cref="Fix"/> is set.
    /// </summary>
    [Parameter(HelpMessage = "Path to the persistent Replace All mapping file.")]
    public string? ReplacementDictionaryPath { get; set; }

    private readonly List<string> _inputPaths = new();
    private IPathResolver? _pathResolver;
    private ISubtitleSpellCheckService? _spellCheckService;

    private IPathResolver PathResolver =>
        _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    private ISubtitleSpellCheckService SpellCheckService =>
        _spellCheckService ??= ModuleServices.GetRequiredService<ISubtitleSpellCheckService>();

    protected override void Begin()
    {
        Logger.LogDebug("Test-SubtitleSpelling Begin (Fix={Fix})", Fix.IsPresent);
    }

    protected override void Process()
    {
        SubtitlePathResolutionHelper.CollectInputPaths(InputPath, _inputPaths);
    }

    protected override void End()
    {
        if (_inputPaths.Count == 0)
        {
            WriteWarning("No input path(s) provided.");
            return;
        }

        var resolvedPairs = SubtitlePathResolutionHelper.ResolveFileOrDirectoryPaths(
            CmdletIO.Paths,
            _inputPaths,
            Logger,
            WriteError);
        if (resolvedPairs.Count == 0)
        {
            WriteWarning("No existing file or directory paths could be resolved.");
            return;
        }

        string? resolvedDictionaryPath = null;
        if (!string.IsNullOrWhiteSpace(DictionaryPath))
        {
            if (!TryResolveInputPath(PathResolver, DictionaryPath!, out resolvedDictionaryPath))
                return;
        }

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var srtPaths = new List<string>();

        foreach (var (resolvedPath, isDirectory) in resolvedPairs)
        {
            if (isDirectory)
            {
                var files = SubtitlePathResolutionHelper.EnumerateMatchingPaths(
                    [(resolvedPath, true)],
                    searchOption,
                    "*.srt",
                    SubtitlePathHelper.IsSrtPath);
                srtPaths.AddRange(files);
                continue;
            }

            if (!SubtitlePathHelper.IsSrtPath(resolvedPath))
            {
                WriteWarning($"Skipping non-SRT path: {resolvedPath}");
                continue;
            }

            srtPaths.Add(resolvedPath);
        }

        if (srtPaths.Count == 0)
        {
            WriteWarning("No SRT files found at the given path(s).");
            return;
        }

        var includeSuggestions = IncludeSuggestions.IsPresent || Fix.IsPresent;

        var ignoreDictionary = ResolveIgnoreDictionary();
        var replacementDictionary = ResolveReplacementDictionary();
        var sessionIgnoreWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in ignoreDictionary.LoadWords())
            sessionIgnoreWords.Add(word);

        if (IgnoreWord is not null)
        {
            foreach (var word in IgnoreWord)
            {
                if (!string.IsNullOrWhiteSpace(word))
                    sessionIgnoreWords.Add(word.Trim());
            }
        }

        var options = new SubtitleSpellCheckOptions(
            [.. sessionIgnoreWords],
            resolvedDictionaryPath,
            includeSuggestions);

        SubtitleSpellFixSession? fixSession = null;
        if (Fix.IsPresent)
        {
            var prompter = ModuleServices.GetService<ISubtitleSpellFixPrompter>()
                ?? new HostSubtitleSpellFixPrompter(this);
            fixSession = new SubtitleSpellFixSession(
                SpellCheckService,
                prompter,
                ignoreDictionary,
                replacementDictionary);
        }

        foreach (var srtPath in srtPaths)
        {
            try
            {
                if (Fix.IsPresent)
                {
                    var displayName = Path.GetFileName(srtPath);
                    if (!ShouldProcess(srtPath, $"Interactively fix subtitle spelling in '{displayName}'"))
                        continue;

                    var wrote = fixSession!.FixFile(srtPath, options, sessionIgnoreWords);
                    if (wrote)
                        WriteVerbose($"Wrote spelling fixes to '{srtPath}'.");
                }

                var reportOptions = new SubtitleSpellCheckOptions(
                    [.. sessionIgnoreWords],
                    resolvedDictionaryPath,
                    includeSuggestions);
                var issues = SpellCheckService.CheckFile(srtPath, reportOptions);
                WriteObject(issues, enumerateCollection: true);
            }
            catch (Exception ex)
            {
                WriteStandardError(ex, ErrorIds.SubtitleSpellCheckFailed, ErrorCategory.ReadError, srtPath);
            }
        }
    }

    private ISubtitleSpellIgnoreDictionary ResolveIgnoreDictionary()
    {
        var logger = ModuleServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger<SubtitleSpellIgnoreDictionary>();

        if (!string.IsNullOrWhiteSpace(IgnoreDictionaryPath))
        {
            var resolvedIgnorePath = PathResolver.TryResolveOutputPath(IgnoreDictionaryPath!, out var resolved)
                ? resolved
                : Path.GetFullPath(IgnoreDictionaryPath!);
            return new SubtitleSpellIgnoreDictionary(logger, resolvedIgnorePath);
        }

        return ModuleServices.GetService<ISubtitleSpellIgnoreDictionary>()
            ?? new SubtitleSpellIgnoreDictionary(logger);
    }

    private ISubtitleSpellReplacementDictionary ResolveReplacementDictionary()
    {
        var logger = ModuleServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger<SubtitleSpellReplacementDictionary>();

        if (!string.IsNullOrWhiteSpace(ReplacementDictionaryPath))
        {
            var resolvedPath = PathResolver.TryResolveOutputPath(ReplacementDictionaryPath!, out var resolved)
                ? resolved
                : Path.GetFullPath(ReplacementDictionaryPath!);
            return new SubtitleSpellReplacementDictionary(logger, resolvedPath);
        }

        return ModuleServices.GetService<ISubtitleSpellReplacementDictionary>()
            ?? new SubtitleSpellReplacementDictionary(logger);
    }
}
