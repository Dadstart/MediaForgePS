using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.BonusProcessing;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts bonus MKV files, extracts subtitles, and organizes them into Plex-style bonus content folders.
/// </summary>
/// <remarks>
/// Three-step workflow: (1) convert bonus MKV files (names ending with -trailer, -featurette, etc.) to MP4,
/// (2) extract English subtitles and optionally OCR image-based tracks (-Ocr Auto/Skip/Force),
/// (3) move converted MP4 and matching .srt/.vtt files into Plex bonus folders under OutputPath.
/// Existing destination files are skipped.
/// Writes a <see cref="MediaConversionResult"/> per converted bonus file to the pipeline.
/// After conversion, writes a <see cref="MediaConversionStatistics"/> with averages for completed files.
/// When subtitles are extracted, also writes a <see cref="SubtitleProcessingResult"/> with extract/OCR counts.
/// Supports -WhatIf and -Confirm.
/// </remarks>
[Cmdlet(VerbsLifecycle.Invoke, "BonusFileProcessing", SupportsShouldProcess = true)]
[OutputType(typeof(MediaConversionResult))]
[OutputType(typeof(MediaConversionStatistics))]
[OutputType(typeof(SubtitleProcessingResult))]
public class InvokeBonusFileProcessingCommand : ProgressCmdletBase
{
    protected override bool ShouldSetCommandTerminalTitle => true;

    private readonly List<MediaConversionResult> _conversionResults = new();

    private IPathResolver? _pathResolverService;
    private IImageSubtitleOcrConverter? _ocrConverter;
    private IBonusProcessingService? _bonusProcessingService;

    /// <summary>
    /// Source directory containing media files to process.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        HelpMessage = "Source directory containing media files to process")]
    [ValidateNotNullOrEmpty]
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Destination directory for organized Plex files.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        HelpMessage = "Destination directory for organized Plex files")]
    [ValidateNotNullOrEmpty]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Default encoder to use: 'x264' (libx264), 'x265' (libx265), or 'nvenc' (NVENC HEVC).
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Default encoder to use when converting bonus files: 'x264', 'x265', or 'nvenc'")]
    [ValidateSet("x264", "x265", "nvenc", IgnoreCase = true)]
    public string DefaultVideoEncoder { get; set; } = "nvenc";

    /// <summary>
    /// When specified, skips extracting subtitles from bonus files.
    /// </summary>
    [Parameter(HelpMessage = "Skip subtitle extraction from bonus files.")]
    public SwitchParameter SkipSubtitles { get; set; }

    /// <summary>
    /// Controls OCR of image-based subtitles (SUP, SUB). Default is Auto. Skip leaves exported subtitles unchanged; Force OCRs all image subtitle files; Auto OCRs image subtitles when the source has a single exported subtitle format and it is not SRT.
    /// </summary>
    [Parameter(HelpMessage = "OCR mode for image subtitles: Auto, Skip, or Force.")]
    [ValidateSet(SubtitleOcrMode.Auto, SubtitleOcrMode.Skip, SubtitleOcrMode.Force, IgnoreCase = true)]
    public string Ocr { get; set; } = SubtitleOcrMode.Default;

    /// <summary>
    /// When specified, skips repair of OCR-produced SRT files after extraction or OCR.
    /// </summary>
    [Parameter(HelpMessage = "Skip SRT repair after extraction or OCR.")]
    public SwitchParameter SkipRepair { get; set; }

    /// <summary>
    /// Keeps source .sup/.sub/.idx files after successful OCR conversion, and keeps unused image sidecars Auto would otherwise discard when a text SRT is already present. By default they are deleted.
    /// </summary>
    [Parameter(HelpMessage = "Keep source image subtitle files after successful OCR conversion.")]
    public SwitchParameter KeepSource { get; set; }

    /// <summary>
    /// Overwrites converted output files when they already exist.
    /// </summary>
    [Parameter(HelpMessage = "Overwrites converted output files when they already exist.")]
    public SwitchParameter Force { get; set; }

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Only used when repair runs.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy SRT files to before repairing; preserves path structure.")]
    public string? BackupPath { get; set; }

    /// <summary>
    /// Maximum number of image-to-SRT conversions to run in parallel when -Ocr is Force or Auto. Default is 10.
    /// </summary>
    [Parameter(HelpMessage = "Maximum number of image subtitle conversions to run simultaneously when OCR is enabled.")]
    public int ThrottleLimit { get; set; } = 10;

    private IPathResolver PathResolverService => _pathResolverService ??= ModuleServices.GetRequiredService<IPathResolver>();

    private IImageSubtitleOcrConverter OcrConverter => _ocrConverter ??= ModuleServices.GetRequiredService<IImageSubtitleOcrConverter>();

    private IBonusProcessingService BonusProcessingService => _bonusProcessingService ??= ModuleServices.GetRequiredService<IBonusProcessingService>();

    /// <summary>
    /// Executes the bonus file processing workflow.
    /// </summary>
    protected override void Process()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath))
            return;

        if (!TryResolveDirectoryPath(InputPath, requireExists: true, out var inputFullPath))
        {
            WriteError(CreateErrorRecord(
                new DirectoryNotFoundException($"Input path does not exist or could not be resolved: '{InputPath}'"),
                "InputPathNotFound",
                ErrorCategory.InvalidArgument,
                InputPath));
            return;
        }

        if (!TryResolveOutputPath(PathResolverService, OutputPath, out var outputFullPath))
            return;

        if (!ShouldProcess(outputFullPath, $"Process bonus files from '{inputFullPath}'"))
            return;

        WriteHostMessage("Starting Bonus File Processing", ConsoleColor.Cyan);
        WriteHostMessage($"  Input:  {inputFullPath}", ConsoleColor.Gray);
        WriteHostMessage($"  Output: {outputFullPath}", ConsoleColor.Gray);

        Directory.CreateDirectory(outputFullPath);
        WriteHostMessage($"Output path ready: {outputFullPath}", ConsoleColor.Green);

        _conversionResults.Clear();

        int bonusFileCount;
        try
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage("Step 1: Converting media files...", ConsoleColor.Cyan);

            var bonusMkvPaths = BonusProcessingService.GetBonusMkvPaths(inputFullPath);
            bonusFileCount = bonusMkvPaths.Count;

            if (bonusFileCount == 0)
            {
                var suffixList = string.Join(", ", BonusProcessingService.PlexLayout.Select(entry => entry.Suffix));
                WriteHostMessage($"No bonus-suffix MKV files to convert (suffixes: {suffixList})", ConsoleColor.Gray);
            }
            else
            {
                MediaConversionHelper.BuildItemsWithSizes(bonusMkvPaths, static path => path, out var totalBytes);
                WriteHostMessage(
                    $"Converting {bonusFileCount} bonus file(s) (total size: {MediaConversionHelper.FormatByteCount(totalBytes)})",
                    ConsoleColor.Cyan);

                var conversionPhaseResult = BonusProcessingService.InvokeConversionPhase(
                    CmdletIO,
                    new BonusConversionRequest(inputFullPath, DefaultVideoEncoder, Force.IsPresent),
                    WriteObject,
                    StoppingToken);

                _conversionResults.AddRange(conversionPhaseResult.Results);
            }

            WriteHostMessage("Media files converted successfully", ConsoleColor.Green);

            if (_conversionResults.Count > 0)
                WriteConversionSummary();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to convert bonus media files");
            WriteError(new ErrorRecord(
                ex,
                "BonusConversionFailed",
                ErrorCategory.OperationStopped,
                inputFullPath));
            WriteWarning("Continuing with file organization for Plex despite conversion error.");
            bonusFileCount = _conversionResults.Count(MediaConversionHelper.IsCompletedConversion);
        }

        if (!SkipSubtitles.IsPresent)
        {
            try
            {
                WriteHostMessage(string.Empty);
                WriteHostMessage("Step 2: Extracting subtitles from bonus files...", ConsoleColor.Cyan);

                var bonusMkvCount = BonusProcessingService.GetBonusMkvPaths(inputFullPath).Count;
                if (bonusMkvCount > 0)
                    WriteHostMessage($"Extracting subtitles from {bonusMkvCount} bonus file(s)...", ConsoleColor.Cyan);

                var exportedPaths = BonusProcessingService.InvokeCaptionExtractionPhase(
                    CmdletIO,
                    new BonusCaptionExtractionRequest(inputFullPath),
                    StoppingToken);

                IReadOnlyList<string> convertedPaths = Array.Empty<string>();
                if (exportedPaths.Count > 0 && SubtitleOcrMode.RequiresOcrProcessing(Ocr))
                {
                    var imagePaths = SubtitlePathHelper.SelectImagePathsForOcr(exportedPaths, Ocr);
                    if (imagePaths.Count > 0)
                    {
                        var srtPaths = SubtitlePathHelper.GetSrtPaths(exportedPaths);
                        var ocrResult = SubtitleOcrRepairWorkflow.Run(
                            CmdletIO,
                            Logger,
                            OcrConverter,
                            PathResolverService,
                            imagePaths,
                            srtPaths,
                            performOcr: true,
                            ThrottleLimit,
                            shouldRepair: SubtitleOcrMode.ShouldRepair(Ocr, SkipRepair.IsPresent),
                            BackupPath,
                            StoppingToken,
                            KeepSource.IsPresent);

                        if (ocrResult != null)
                            convertedPaths = ocrResult.ConvertedSrtPaths;
                    }

                    ImageSubtitleConversionHelper.DeleteUnusedImageSubtitleSources(
                        exportedPaths,
                        Ocr,
                        KeepSource.IsPresent,
                        Logger);
                }

                var subtitleResult = SubtitleProcessingResult.Create(exportedPaths, convertedPaths);
                WriteHostMessage(
                    $"Subtitle extraction completed: {subtitleResult.ExtractedCount} extracted, {subtitleResult.ConvertedCount} converted.",
                    ConsoleColor.Green);
                WriteObject(subtitleResult);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to extract or process subtitles from bonus files");
                WriteWarning($"Continuing with file organization despite subtitle error: {ex.Message}");
            }
        }

        try
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage("Step 3: Organizing files for Plex...", ConsoleColor.Cyan);
            var organizationResult = BonusProcessingService.InvokeOrganizationPhase(
                CmdletIO,
                new BonusOrganizationRequest(inputFullPath, outputFullPath),
                StoppingToken);
            WriteHostMessage(
                $"Moved {organizationResult.FilesMoved} of {organizationResult.MoveCandidates} Plex file(s)",
                ConsoleColor.Green);
            WriteHostMessage("Files successfully organized and moved to Plex location", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to organize files for Plex");
            var error = new ErrorRecord(
                ex,
                "PlexOrganizationFailed",
                ErrorCategory.OperationStopped,
                outputFullPath);
            ThrowTerminatingError(error);
            return;
        }

        WriteHostMessage(string.Empty);
        WriteHostMessage("Bonus File Processing completed successfully!", ConsoleColor.Green);
        WriteHostMessage($"  Bonus files processed: {bonusFileCount}", ConsoleColor.Gray);
    }

    private bool TryResolveDirectoryPath(string path, bool requireExists, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (PathResolver.TryResolveProviderPath(CmdletIO.Paths, path, out var fromProvider))
        {
            resolvedPath = fromProvider!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        if (PathResolver.TryGetUnresolvedProviderPath(CmdletIO.Paths, path, out var unresolved))
        {
            resolvedPath = unresolved!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        return false;
    }

    private void WriteConversionSummary()
    {
        var succeeded = _conversionResults.Where(MediaConversionHelper.IsCompletedConversion).ToList();
        var failed = _conversionResults.Where(r => !MediaConversionHelper.IsCompletedConversion(r)).ToList();

        if (succeeded.Count > 0)
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage($"  ✅ Succeeded ({succeeded.Count}):", ConsoleColor.Green);
            foreach (var result in succeeded)
                WriteHostMessage($"    {MediaConversionHelper.FormatConversionResultLine(result)}", ConsoleColor.Gray);
        }

        if (failed.Count > 0)
        {
            WriteHostMessage(string.Empty);
            WriteHostMessage($"  ❌ Failed ({failed.Count}):", ConsoleColor.Red);
            foreach (var result in failed)
                WriteHostMessage($"    {MediaConversionHelper.FormatConversionResultLine(result)}", ConsoleColor.Gray);
        }

        var statistics = MediaConversionHelper.CreateConversionStatistics(_conversionResults);
        if (statistics.FileCount <= 0)
            return;

        WriteHostMessage(string.Empty);
        WriteHostMessage($"  {MediaConversionHelper.FormatConversionStatisticsLine(statistics)}", ConsoleColor.Cyan);
        WriteObject(statistics);
    }
}
