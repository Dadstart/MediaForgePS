using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using PathResolverImpl = Dadstart.Labs.MediaForge.Services.System.PathResolver;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts image-based subtitle files (SUP, SUB) to SRT via OCR, then repairs all SRT files (including any SRT files in the input list). For use when subtitles are already extracted to disk.
/// </summary>
/// <remarks>
/// Equivalent to running Convert-ImageSubtitlesToSrt on SUP/SUB paths then Repair-Subtitles on all SRT paths. Requires Subtitle Edit (and Tesseract) when any input is SUP or SUB. Output SRT paths are written to the pipeline.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "SubtitleOcrRepair")]
[OutputType(typeof(string))]
public class InvokeSubtitleOcrRepairCommand : CmdletBase
{
    /// <summary>
    /// Path(s) to .sup, .sub, or .srt file(s), or directory/directories containing them. Pipeline accepts path strings.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path(s) to SUP, SUB, or SRT file(s) or directory/directories containing them.")]
    [Alias("Path")]
    public string[] InputPath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Directory structure is preserved under this root.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy SRT files to before repairing; preserves path structure.")]
    public string? BackupPath { get; set; }

    /// <summary>
    /// Maximum number of image-to-SRT conversions to run in parallel. Default is 10.
    /// </summary>
    [Parameter(HelpMessage = "Maximum number of image subtitle conversions to run simultaneously.")]
    public int ThrottleLimit { get; set; } = 10;

    /// <summary>
    /// When specified, skips the SRT repair step. Only conversion (SUP/SUB to SRT) is performed; converted and existing SRT paths are still written to the pipeline.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair; only convert image subtitles to SRT.")]
    public SwitchParameter NoRepair { get; set; }

    /// <summary>
    /// When input is a directory, recurse into subdirectories to find .sup, .sub, and .srt files.
    /// </summary>
    [Parameter(HelpMessage = "When input is a directory, recurse into subdirectories.")]
    public SwitchParameter Recurse { get; set; }

    private readonly List<string> _inputPaths = new();
    private IExecutableService? _executableService;
    private IPathResolver? _pathResolver;

    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    protected override void Begin()
    {
        Logger.LogDebug("Invoke-SubtitleOcrRepair Begin");
    }

    protected override void Process()
    {
        if (InputPath == null || InputPath.Length == 0)
            return;
        foreach (var p in InputPath)
        {
            if (!string.IsNullOrWhiteSpace(p))
                _inputPaths.Add(p.Trim());
        }
    }

    protected override void End()
    {
        if (_inputPaths.Count == 0)
        {
            WriteWarning("No input path(s) provided.");
            return;
        }

        var resolvedPairs = PathResolverImpl.ResolveFileOrDirectoryPaths(this, _inputPaths, Logger, e => WriteError(e));
        if (resolvedPairs.Count == 0)
        {
            WriteWarning("No existing file or directory paths could be resolved.");
            return;
        }

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var allSubtitlePaths = new List<string>();
        foreach (var (resolvedPath, isDirectory) in resolvedPairs)
        {
            if (isDirectory)
            {
                var files = Directory
                    .EnumerateFiles(resolvedPath, "*.*", searchOption)
                    .Where(f => SubtitlePathHelper.IsImageSubtitlePath(f) || SubtitlePathHelper.IsSrtPath(f))
                    .ToList();
                allSubtitlePaths.AddRange(files);
            }
            else if (SubtitlePathHelper.IsImageSubtitlePath(resolvedPath) || SubtitlePathHelper.IsSrtPath(resolvedPath))
                allSubtitlePaths.Add(resolvedPath);
        }

        var imagePaths = SubtitlePathHelper.GetImageSubtitlePaths(allSubtitlePaths);
        var srtPathsFromInput = SubtitlePathHelper.GetSrtPaths(allSubtitlePaths);

        if (imagePaths.Count == 0 && srtPathsFromInput.Count == 0)
        {
            WriteWarning("No SUP, SUB, or SRT files found at the given path(s).");
            return;
        }

        WriteHostMessage($"Processing {imagePaths.Count} image subtitle(s) and {srtPathsFromInput.Count} SRT file(s).", ConsoleColor.Cyan);

        IReadOnlyList<string> convertedSrtPaths = Array.Empty<string>();
        if (imagePaths.Count > 0)
        {
            var subtitleEditPath = WindowsExecutablePathHelper.GetSubtitleEditPath();
            if (string.IsNullOrEmpty(subtitleEditPath))
            {
                WriteError(CreateErrorRecord(
                    new FileNotFoundException($"Subtitle Edit not found (required to convert {imagePaths.Count} image subtitle(s)). Expected: {WindowsExecutablePathHelper.GetSubtitleEditExpectedPath()}"),
                    "SubtitleEditNotFound",
                    ErrorCategory.ObjectNotFound,
                    null));
                return;
            }
            _ = ExecutableService;
            convertedSrtPaths = ImageSubtitleConversionHelper.ConvertImagePathsToSrtParallel(
                this, ExecutableService, Logger, subtitleEditPath, imagePaths, Math.Max(1, ThrottleLimit), WriteError);
        }

        var allSrtPaths = srtPathsFromInput.Concat(convertedSrtPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (allSrtPaths.Count == 0)
        {
            WriteHostMessage("No SRT files to repair.", ConsoleColor.Green);
            return;
        }

        var shouldRepair = !NoRepair.IsPresent;
        SrtRepairHelper.RunRepairLoop(this, Logger, PathResolver, allSrtPaths, shouldRepair, BackupPath, WriteObject);

        WriteHostMessage("OCR and repair completed.", ConsoleColor.Green);
    }

}
