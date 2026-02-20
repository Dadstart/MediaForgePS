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
/// Fixes common OCR errors in SRT subtitle files (music note ♪ misreads, pipe as I, unmatched brackets, etc.).
/// </summary>
/// <remarks>
/// Can process a single file, multiple files, or a directory of .srt files. When processing a directory, all .srt files
/// are fixed in place. When processing a single file, use -OutputPath to write to a different file; otherwise the file is overwritten in place.
/// </remarks>
[Cmdlet(VerbsDiagnostic.Repair, "Subtitles")]
[OutputType(typeof(string))]
public class RepairSubtitlesCommand : CmdletBase
{
    /// <summary>
    /// Path to an SRT file or directory containing .srt files. Pipeline accepts path strings.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path to SRT file(s) or directory containing .srt files.")]
    [Alias("Path")]
    public string[] InputPath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Output path for the fixed SRT. Only used when a single file is processed; ignored for directories or multiple paths.
    /// </summary>
    [Parameter(Position = 1, HelpMessage = "Output path when processing a single file. Omit to overwrite in place.")]
    public string? OutputPath { get; set; }

    /// <summary>
    /// When input is a directory, recurse into subdirectories to find .srt files.
    /// </summary>
    [Parameter(HelpMessage = "When input is a directory, recurse into subdirectories.")]
    public SwitchParameter Recurse { get; set; }

    private readonly List<string> _inputPaths = new();
    private IPathResolver? _pathResolver;

    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    protected override void Begin()
    {
        Logger.LogDebug("Repair-Subtitles Begin");
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

        var resolvedPairs = ResolveInputPaths();
        if (resolvedPairs.Count == 0)
        {
            WriteWarning("No existing file or directory paths could be resolved.");
            return;
        }

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var writtenPaths = new List<string>();
        var totalPaths = resolvedPairs.Count;
        var pathIndex = 0;

        foreach (var (resolvedPath, isDirectory) in resolvedPairs)
        {
            pathIndex++;
            var pathDisplayName = Path.GetFileName(resolvedPath) ?? resolvedPath;
            var mainPercent = totalPaths > 0 ? (int)((pathIndex * 100.0) / totalPaths) : 0;
            var mainStatus = $"Path {pathIndex} of {totalPaths} ({mainPercent}%) — {pathDisplayName}";
            MediaConversionHelper.WriteMainProgress(this, "Repairing subtitles", mainStatus, mainPercent, recordType: ProgressRecordType.Processing);

            if (isDirectory)
            {
                var files = Directory.EnumerateFiles(resolvedPath, "*.srt", searchOption).ToList();
                foreach (var filePath in files)
                {
                    if (ProcessFile(filePath, filePath))
                        writtenPaths.Add(filePath);
                }
            }
            else
            {
                var outputPath = resolvedPairs.Count == 1 && _inputPaths.Count == 1 && !string.IsNullOrWhiteSpace(OutputPath)
                    ? ResolveOutputPath(OutputPath!)
                    : resolvedPath;
                if (outputPath != null && ProcessFile(resolvedPath, outputPath))
                    writtenPaths.Add(outputPath);
            }
        }

        MediaConversionHelper.WriteProgressCompleted(this, "Repairing subtitles", "Current file");

        foreach (var path in writtenPaths)
            WriteObject(path);
    }

    private List<(string ResolvedPath, bool IsDirectory)> ResolveInputPaths()
    {
        var result = new List<(string, bool)>();
        foreach (var path in _inputPaths)
        {
            if (PathResolverImpl.TryResolveProviderPath(this, path, out var resolved))
            {
                if (Directory.Exists(resolved))
                    result.Add((resolved!, true));
                else if (File.Exists(resolved))
                    result.Add((resolved!, false));
                else
                    Logger.LogDebug("Resolved path does not exist: {Path}", resolved);
            }
            else if (PathResolverImpl.TryGetUnresolvedProviderPath(this, path, out var unresolved))
            {
                if (Directory.Exists(unresolved))
                    result.Add((unresolved!, true));
                else if (File.Exists(unresolved))
                    result.Add((unresolved!, false));
                else
                    WriteError(CreateErrorRecord(new FileNotFoundException("File or directory not found.", path), "PathNotFound", ErrorCategory.ObjectNotFound, path));
            }
            else
                WriteError(CreateErrorRecord(new FileNotFoundException("File or directory not found.", path), "PathNotFound", ErrorCategory.ObjectNotFound, path));
        }

        return result;
    }

    private string? ResolveOutputPath(string outputPath)
    {
        if (PathResolver.TryResolveOutputPath(outputPath, out var resolved))
            return resolved;
        WriteError(CreateErrorRecord(new InvalidOperationException($"Failed to resolve output path: {outputPath}"), "OutputPathResolutionFailed", ErrorCategory.InvalidArgument, outputPath));
        return null;
    }

    private bool ProcessFile(string inputPath, string outputPath)
    {
        try
        {
            var content = File.ReadAllText(inputPath).Replace("\r\n", "\n").Replace("\r", "\n");
            var fixedContent = SrtOcrFixHelper.FixMusicNoteOcrErrors(content);
            File.WriteAllText(outputPath, fixedContent, System.Text.Encoding.UTF8);
            WriteVerbose($"Wrote fixed subtitles to: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to repair subtitles: {Path}", inputPath);
            WriteError(CreateErrorRecord(ex, "RepairSubtitlesFailed", ErrorCategory.WriteError, inputPath));
            return false;
        }
    }
}
