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
/// Converts image-based subtitle files (SUP, SUB) to SRT (text) using Subtitle Edit with Tesseract OCR.
/// </summary>
/// <remarks>
/// Requires Subtitle Edit installed in "%ProgramFiles%\\Subtitle Edit" and Tesseract OCR.
/// Processes .sup and .sub files directly or directories containing these files. Output SRT files are written
/// next to each input file unless -OutputPath is specified for a single file.
/// </remarks>
[Cmdlet(VerbsData.Convert, "ImageSubtitlesToSrt")]
[Alias("Convert-SupToSrt")]
[OutputType(typeof(string))]
public class ConvertImageSubtitlesToSrtCommand : CmdletBase
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

        var resolvedPairs = PathResolverImpl.ResolveFileOrDirectoryPaths(this, _inputPaths, Logger, e => WriteError(e));
        if (resolvedPairs.Count == 0)
        {
            WriteWarning("No existing file or directory paths could be resolved.");
            return;
        }

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var writtenPaths = new List<string>();
        var totalPaths = resolvedPairs.Count;
        var pathIndex = 0;

        foreach (var pair in resolvedPairs)
        {
            pathIndex++;
            var (resolvedPath, isDirectory) = pair;
            var pathDisplayName = Path.GetFileName(resolvedPath) ?? resolvedPath;
            var mainPercent = totalPaths > 0 ? (int)((pathIndex * 100.0) / totalPaths) : 0;
            var mainStatus = $"Path {pathIndex} of {totalPaths} ({mainPercent}%) — {pathDisplayName}";
            MediaConversionHelper.WriteMainProgress(this, "Converting image subtitles to SRT", mainStatus, mainPercent, recordType: ProgressRecordType.Processing);

            if (isDirectory)
            {
                var files = Directory
                    .EnumerateFiles(resolvedPath, "*.*", searchOption)
                    .Where(SubtitlePathHelper.IsImageSubtitlePath)
                    .ToList();
                var fileCount = files.Count;
                var currentFileIndex = 0;
                foreach (var filePath in files)
                {
                    currentFileIndex++;
                    var filePercent = fileCount > 0 ? (int)((currentFileIndex * 100.0) / fileCount) : 0;
                    MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Converting...", Path.GetFileName(filePath), percentComplete: filePercent, recordType: ProgressRecordType.Processing);
                    var srtPath = Path.ChangeExtension(filePath, "srt") ?? filePath + ".srt";
                    if (ConvertFile(subtitleEditPath, filePath, srtPath))
                        writtenPaths.Add(srtPath);
                    MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Completed", Path.GetFileName(filePath), percentComplete: filePercent, recordType: ProgressRecordType.Completed);
                }
            }
            else
            {
                MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Converting...", pathDisplayName, percentComplete: 100, recordType: ProgressRecordType.Processing);
                var defaultOutput = Path.ChangeExtension(resolvedPath, "srt") ?? resolvedPath + ".srt";
                var outputPath = resolvedPairs.Count == 1 && _inputPaths.Count == 1 && !string.IsNullOrWhiteSpace(OutputPath)
                    ? PathResolverImpl.ResolveOutputPathOrNull(PathResolver, OutputPath!, e => WriteError(e))
                    : defaultOutput;
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
            ImageSubtitleConversionHelper.ConvertToSrt(ExecutableService, subtitleEditPath, inputSubtitlePath, outputSrtPath);
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
