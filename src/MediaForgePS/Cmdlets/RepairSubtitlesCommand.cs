using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>
    /// Directory to copy all SRT files to before repairing. Directory structure under each input path is preserved.
    /// </summary>
    [Parameter(HelpMessage = "Directory to copy all files to before repairing; preserves directory structure.")]
    public string? BackupPath { get; set; }

    private readonly List<string> _inputPaths = new();
    private IPathResolver? _pathResolver;

    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    protected override void Begin()
    {
        Logger.LogDebug("Repair-Subtitles Begin");
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

        var resolvedPairs = SubtitlePathResolutionHelper.ResolveFileOrDirectoryPaths(this, _inputPaths, Logger, WriteError);
        if (resolvedPairs.Count == 0)
        {
            WriteWarning("No existing file or directory paths could be resolved.");
            return;
        }

        string? singleOutputPath = null;
        if (resolvedPairs.Count == 1 &&
            !resolvedPairs[0].IsDirectory &&
            _inputPaths.Count == 1 &&
            !string.IsNullOrWhiteSpace(OutputPath))
        {
            singleOutputPath = PathResolverImpl.ResolveOutputPathOrNull(PathResolver, OutputPath!, WriteError);
            if (singleOutputPath == null)
                return;
        }

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var repairItems = new List<SrtRepairHelper.SrtRepairItem>();

        foreach (var (resolvedPath, isDirectory) in resolvedPairs)
        {
            var pathDisplayName = Path.GetFileName(resolvedPath) ?? resolvedPath;
            if (isDirectory)
            {
                var files = SubtitlePathResolutionHelper.EnumerateMatchingPaths(
                    [(resolvedPath, true)],
                    searchOption,
                    "*.srt",
                    SubtitlePathHelper.IsSrtPath);

                foreach (var filePath in files)
                {
                    var relativePath = Path.Combine(pathDisplayName, Path.GetRelativePath(resolvedPath, filePath));
                    repairItems.Add(new SrtRepairHelper.SrtRepairItem(filePath, filePath, relativePath));
                }
            }
            else
            {
                if (!SubtitlePathHelper.IsSrtPath(resolvedPath))
                    continue;

                repairItems.Add(new SrtRepairHelper.SrtRepairItem(
                    resolvedPath,
                    singleOutputPath ?? resolvedPath,
                    pathDisplayName));
            }
        }

        if (repairItems.Count == 0)
        {
            WriteWarning("No SRT files found at the given path(s).");
            return;
        }

        SrtRepairHelper.RunRepairLoop(
            this,
            Logger,
            PathResolver,
            repairItems,
            shouldRepair: true,
            BackupPath,
            WriteObject);
    }
}
