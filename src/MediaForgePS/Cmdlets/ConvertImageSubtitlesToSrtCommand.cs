using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts image-based subtitle files (SUP, SUB) to SRT using Subtitle Edit with Tesseract OCR.
/// </summary>
/// <remarks>
/// Alias: Convert-SupToSrt. Writes created SRT file paths to the pipeline.
/// Requires Subtitle Edit under %ProgramFiles%\Subtitle Edit and Tesseract OCR.
/// </remarks>
[Cmdlet(VerbsData.Convert, "ImageSubtitlesToSrt")]
[Alias("Convert-SupToSrt")]
[OutputType(typeof(string))]
public class ConvertImageSubtitlesToSrtCommand : ProgressCmdletBase
{
    /// <summary>
    /// Path to a .sup or .sub file, or directory containing .sup/.sub files. Pipeline accepts path strings.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path to .sup/.sub file(s) or directory containing .sup/.sub files.")]
    [Alias("Path")]
    public string[] InputPath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Output path for the SRT file. Only used when a single file is processed; ignored for directories or multiple paths.
    /// </summary>
    [Parameter(Position = 1, HelpMessage = "Output path when processing a single file. Omit to write .srt next to the source file.")]
    public string? OutputPath { get; set; }

    /// <summary>
    /// When input is a directory, recurse into subdirectories to find .sup and .sub files.
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
        Logger.LogDebug("Convert-ImageSubtitlesToSrt Begin");
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

        var subtitleEditPath = WindowsExecutablePathHelper.GetSubtitleEditPath();
        if (string.IsNullOrEmpty(subtitleEditPath))
        {
            WriteError(CreateErrorRecord(
                new FileNotFoundException($"Subtitle Edit not found. Expected: {WindowsExecutablePathHelper.GetSubtitleEditExpectedPath()}"),
                "SubtitleEditNotFound",
                ErrorCategory.ObjectNotFound,
                null));
            return;
        }

        var resolvedPairs = SubtitlePathResolutionHelper.ResolveFileOrDirectoryPaths(this, _inputPaths, Logger, WriteError);
        if (resolvedPairs.Count == 0)
        {
            WriteWarning("No existing file or directory paths could be resolved.");
            return;
        }

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var totalFiles = 0;
        foreach (var pair in resolvedPairs)
        {
            var (resolvedPath, isDirectory) = pair;
            if (isDirectory)
                totalFiles += SubtitlePathResolutionHelper.EnumerateMatchingPaths([pair], searchOption, "*.*", SubtitlePathHelper.IsImageSubtitlePath).Count;
            else
                totalFiles += 1;
        }

        var writtenPaths = new List<string>();
        var fileIndex = 0;

        foreach (var pair in resolvedPairs)
        {
            var (resolvedPath, isDirectory) = pair;
            var pathDisplayName = Path.GetFileName(resolvedPath) ?? resolvedPath;

            if (isDirectory)
            {
                var files = SubtitlePathResolutionHelper.EnumerateMatchingPaths(
                    [pair],
                    searchOption,
                    "*.*",
                    SubtitlePathHelper.IsImageSubtitlePath);
                foreach (var filePath in files)
                {
                    fileIndex++;
                    var fileName = Path.GetFileName(filePath) ?? filePath;
                    var mainPercent = totalFiles > 0 ? (int)((fileIndex * 100.0) / totalFiles) : 0;
                    var mainStatus = $"File {fileIndex} of {totalFiles} ({mainPercent}%) — {fileName}";
                    MediaConversionHelper.WriteMainProgress(this, "Converting image subtitles to SRT", mainStatus, mainPercent, recordType: ProgressRecordType.Processing);
                    MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Converting...", fileName, percentComplete: mainPercent, recordType: ProgressRecordType.Processing);
                    var srtPath = Path.ChangeExtension(filePath, "srt") ?? filePath + ".srt";
                    if (ConvertFile(subtitleEditPath, filePath, srtPath))
                        writtenPaths.Add(srtPath);
                    MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Completed", fileName, percentComplete: mainPercent, recordType: ProgressRecordType.Completed);
                }
            }
            else
            {
                fileIndex++;
                var mainPercent = totalFiles > 0 ? (int)((fileIndex * 100.0) / totalFiles) : 0;
                var mainStatus = $"File {fileIndex} of {totalFiles} ({mainPercent}%) — {pathDisplayName}";
                MediaConversionHelper.WriteMainProgress(this, "Converting image subtitles to SRT", mainStatus, mainPercent, recordType: ProgressRecordType.Processing);
                MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Converting...", pathDisplayName, percentComplete: mainPercent, recordType: ProgressRecordType.Processing);
                var defaultOutput = Path.ChangeExtension(resolvedPath, "srt") ?? resolvedPath + ".srt";
                var outputPath = defaultOutput;
                if (resolvedPairs.Count == 1 && _inputPaths.Count == 1 && !string.IsNullOrWhiteSpace(OutputPath))
                {
                    if (!TryResolveOutputPath(PathResolver, OutputPath!, out var resolvedOutputPath))
                        continue;

                    outputPath = resolvedOutputPath;
                }

                if (outputPath != null && ConvertFile(subtitleEditPath, resolvedPath, outputPath))
                    writtenPaths.Add(outputPath);
                MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Completed", pathDisplayName, recordType: ProgressRecordType.Completed);
            }
        }

        MediaConversionHelper.WriteProgressCompleted(this, "Converting image subtitles to SRT", "Current file");

        foreach (var path in writtenPaths)
            WriteObject(path);
    }

    private bool ConvertFile(string subtitleEditPath, string inputSubtitlePath, string outputSrtPath)
    {
        try
        {
            ImageSubtitleConversionHelper.ConvertToSrt(ExecutableService, subtitleEditPath, inputSubtitlePath, outputSrtPath, Logger);
            WriteVerbose($"Converted to: {outputSrtPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to convert image subtitles to SRT: {Path}", inputSubtitlePath);
            WriteError(CreateErrorRecord(ex, "ConvertImageSubtitlesToSrtFailed", ErrorCategory.OperationStopped, inputSubtitlePath));
            return false;
        }
    }
}
