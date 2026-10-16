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
/// Checks SRT subtitle files for misspellings and writes one issue object per finding.
/// </summary>
/// <remarks>
/// Does not modify files. Files with no issues produce no pipeline output.
/// </remarks>
[Cmdlet(VerbsDiagnostic.Test, "SubtitleSpelling")]
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
    /// When set, each issue includes suggested corrections.
    /// </summary>
    [Parameter(HelpMessage = "Include spelling suggestions (slower).")]
    public SwitchParameter IncludeSuggestions { get; set; }

    private readonly List<string> _inputPaths = new();
    private IPathResolver? _pathResolver;
    private ISubtitleSpellCheckService? _spellCheckService;

    private IPathResolver PathResolver =>
        _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    private ISubtitleSpellCheckService SpellCheckService =>
        _spellCheckService ??= ModuleServices.GetRequiredService<ISubtitleSpellCheckService>();

    protected override void Begin()
    {
        Logger.LogDebug("Test-SubtitleSpelling Begin");
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

        var options = new SubtitleSpellCheckOptions(
            IgnoreWord,
            resolvedDictionaryPath,
            IncludeSuggestions.IsPresent);

        foreach (var srtPath in srtPaths)
        {
            try
            {
                var issues = SpellCheckService.CheckFile(srtPath, options);
                WriteObject(issues, enumerateCollection: true);
            }
            catch (Exception ex)
            {
                WriteStandardError(ex, ErrorIds.SubtitleSpellCheckFailed, ErrorCategory.ReadError, srtPath);
            }
        }
    }
}
