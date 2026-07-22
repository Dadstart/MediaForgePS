using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts image-based subtitle files (SUP, SUB) to SRT via OCR, then repairs OCR-produced SRT files.
/// </summary>
/// <remarks>
/// For subtitles already on disk. Equivalent to running <see cref="ConvertImageSubtitlesToSrtCommand"/> on image paths,
/// then <see cref="RepairSubtitlesCommand"/> on OCR-produced SRT paths only. Pre-existing SRT files in the input are not repaired.
/// Requires Subtitle Edit and Tesseract when any input is SUP or SUB.
/// Writes a <see cref="SubtitleProcessingResult"/> with conversion counts to the pipeline.
/// Supports -WhatIf and -Confirm.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "SubtitleOcrRepair", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(SubtitleProcessingResult))]
public class InvokeSubtitleOcrRepairCommand : ProgressCmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

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
    /// When specified, skips repair of OCR-produced SRT files. Only SUP/SUB to SRT conversion is performed.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair; only convert image subtitles to SRT.")]
    public SwitchParameter SkipRepair { get; set; }

    /// <summary>
    /// Keeps source .sup/.sub/.idx files after successful OCR conversion. By default they are deleted.
    /// </summary>
    [Parameter(HelpMessage = "Keep source image subtitle files after successful OCR conversion.")]
    public SwitchParameter KeepSource { get; set; }

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
        SubtitlePathResolutionHelper.CollectInputPaths(InputPath, _inputPaths);
    }

    protected override void End()
    {
        if (_inputPaths.Count == 0)
        {
            WriteWarning("No input path(s) provided.");
            return;
        }

        var resolvedPairs = SubtitlePathResolutionHelper.ResolveFileOrDirectoryPaths(CmdletIO.Paths, _inputPaths, Logger, WriteError);
        if (resolvedPairs.Count == 0)
        {
            WriteWarning("No existing file or directory paths could be resolved.");
            return;
        }

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var allSubtitlePaths = SubtitlePathResolutionHelper.EnumerateMatchingPaths(
            resolvedPairs,
            searchOption,
            "*.*",
            path => SubtitlePathHelper.IsImageSubtitlePath(path) || SubtitlePathHelper.IsSrtPath(path));

        var imagePaths = SubtitlePathHelper.GetImageSubtitlePaths(allSubtitlePaths);
        var srtPathsFromInput = SubtitlePathHelper.GetSrtPaths(allSubtitlePaths);

        if (imagePaths.Count == 0 && srtPathsFromInput.Count == 0)
        {
            WriteWarning("No SUP, SUB, or SRT files found at the given path(s).");
            return;
        }

        var actionDescription = SkipRepair.IsPresent
            ? "Convert image subtitles to SRT"
            : "Convert image subtitles to SRT and repair";
        var approvedImagePaths = imagePaths
            .Where(path => ShouldProcess(path, $"{actionDescription} '{Path.GetFileName(path)}'"))
            .ToList();

        if (imagePaths.Count > 0 && approvedImagePaths.Count == 0)
        {
            WriteHostMessage("No image subtitle files approved for processing.", ConsoleColor.Green);
            WriteObject(SubtitleProcessingResult.Empty);
            return;
        }

        WriteHostMessage($"Processing {approvedImagePaths.Count} image subtitle(s) and {srtPathsFromInput.Count} SRT file(s).", ConsoleColor.Cyan);

        var ocrResult = SubtitleOcrRepairWorkflow.Run(
            CmdletIO,
            Logger,
            ExecutableService,
            PathResolver,
            approvedImagePaths,
            srtPathsFromInput,
            performOcr: true,
            ThrottleLimit,
            shouldRepair: !SkipRepair.IsPresent,
            BackupPath,
            StoppingToken,
            KeepSource.IsPresent);

        if (ocrResult == null)
            return;

        var result = SubtitleProcessingResult.Create(convertedPaths: ocrResult.ConvertedSrtPaths);
        if (ocrResult.AllSrtPaths.Count == 0)
        {
            WriteHostMessage("No SRT files to repair.", ConsoleColor.Green);
            WriteObject(result);
            return;
        }

        WriteHostMessage(
            $"OCR and repair completed: {result.ConvertedCount} converted.",
            ConsoleColor.Green);
        WriteObject(result);
    }

}
