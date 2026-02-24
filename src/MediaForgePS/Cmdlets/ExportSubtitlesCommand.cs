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
/// Extracts English subtitle streams from one or more media files to separate subtitle files.
/// </summary>
/// <remarks>
/// Processes media files to extract subtitle tracks matching English language (Language -like 'en*').
/// Output files follow the naming pattern: [originalname].[streamindex].en.sdh.[ext] or [originalname].en.sdh.[ext].
/// Supports SubRip (SRT), ASS, SSA, WebVTT, DVD subtitle, and HDMV PGS. For dvd_subtitle codec uses mkvextract when available.
/// </remarks>
[Cmdlet(VerbsData.Export, "Subtitles")]
[Alias("Export-Subtitles")]
[OutputType(typeof(void))]
public class ExportSubtitlesCommand : CmdletBase
{
    /// <summary>
    /// Media file path(s) or folder path(s). For folders, all .mkv files are processed. Pipeline accepts path strings or MediaFile objects from Get-MediaFile.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Path(s) to media file(s) or folder(s) containing .mkv files.")]
    [Alias("Path")]
    public object[]? InputPath { get; set; }

    private readonly List<object> _pathOrMediaFiles = new();
    private IMediaReaderService? _mediaReaderService;
    private IExecutableService? _executableService;
    private IPathResolver? _pathResolver;

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();
    private IExecutableService ExecutableService => _executableService ??= ModuleServices.GetRequiredService<IExecutableService>();
    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    protected override void Begin()
    {
        Logger.LogDebug("Export-Subtitles Begin");
    }

    protected override void Process()
    {
        if (InputPath == null || InputPath.Length == 0)
            return;
        foreach (var p in InputPath)
        {
            if (p != null)
                _pathOrMediaFiles.Add(p);
        }
    }

    protected override void End()
    {
        if (_pathOrMediaFiles.Count == 0)
        {
            WriteWarning("No paths or media files provided.");
            return;
        }

        var mediaFiles = SubtitleExportHelper.ResolveMediaFiles(_pathOrMediaFiles, this, MediaReaderService, Logger, e => WriteError(e)).ToList();
        if (mediaFiles.Count == 0)
        {
            WriteWarning("No media files to process.");
            return;
        }

        var filesWithSize = new List<(MediaFile Mf, long Size)>();
        long totalBytes = 0;
        foreach (var mf in mediaFiles)
        {
            long size = 0;
            try
            {
                var fi = new FileInfo(mf.Path);
                if (fi.Exists)
                {
                    size = fi.Length;
                    totalBytes += size;
                }
            }
            catch
            {
                // Use 0 for this file
            }

            filesWithSize.Add((mf, size));
        }

        WriteHostMessage($"Extracting subtitles from {mediaFiles.Count} file(s) (total size: {MediaConversionHelper.FormatByteCount(totalBytes)})", ConsoleColor.Cyan);

        long completedBytes = 0;
        var totalFiles = filesWithSize.Count;
        for (var i = 0; i < filesWithSize.Count; i++)
        {
            var (mf, fileSize) = filesWithSize[i];
            var fileIndex = i + 1;
            var fileName = Path.GetFileName(mf.Path);
            var (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(fileIndex, totalFiles, fileName, completedBytes, totalBytes);
            MediaConversionHelper.WriteMainProgress(this, "Extracting subtitles", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Extracting...", fileName, recordType: ProgressRecordType.Processing);

            ExportSubtitlesForMediaFile(mf, totalFiles, fileIndex);

            completedBytes += fileSize;
            (status, percent) = MediaConversionHelper.BuildBatchProgressStatus(fileIndex, totalFiles, fileName, completedBytes, totalBytes);
            MediaConversionHelper.WriteMainProgress(this, "Extracting subtitles", status, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(this, "Current file", "Completed", fileName, recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(this, "Extracting subtitles", "Current file");

        WriteHostMessage("Subtitle extraction completed", ConsoleColor.Green);
    }

    private void ExportSubtitlesForMediaFile(MediaFile mediaFile, int totalFiles, int fileIndex)
    {
        var fileName = Path.GetFileNameWithoutExtension(mediaFile.Path);
        WriteVerbose($"[{fileIndex}/{totalFiles}] Processing: {fileName}");

        var subtitles = (mediaFile.Streams ?? Array.Empty<MediaStream>())
            .Where(s => string.Equals(s.Type, "subtitle", StringComparison.OrdinalIgnoreCase) &&
                (s.Language ?? "").StartsWith("en", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (subtitles.Count == 0)
        {
            WriteVerbose($"No English subtitles in {fileName}");
            return;
        }

        var subIndex = 0;
        foreach (var sub in subtitles)
        {
            subIndex++;
            var percent = (int)Math.Round((subIndex * 100.0) / subtitles.Count, 0);
            MediaConversionHelper.WriteCurrentItemProgress(this, fileName, $"Stream {sub.Index} ({sub.Codec})", percentComplete: percent);
            ExportSingleSubtitle(sub, mediaFile, subtitles.Count);
        }

        MediaConversionHelper.WriteCurrentItemProgress(this, fileName, "Complete", recordType: ProgressRecordType.Completed);
    }

    private void ExportSingleSubtitle(MediaStream stream, MediaFile mediaFile, int totalSubtitleCount)
    {
        if (!SubtitleExportHelper.CodecToExtension.TryGetValue(stream.Codec ?? "", out var ext))
        {
            WriteWarning($"Unknown codec: {stream.Codec} - using .bin extension");
            ext = "bin";
        }

        var newPath = SubtitleExportHelper.GetOutputPath(mediaFile.Path, stream.Index, totalSubtitleCount, ext);
        if (!PathResolver.TryResolveOutputPath(newPath, out var resolvedOutput))
        {
            WriteError(new ErrorRecord(new InvalidOperationException($"Failed to resolve output path: {newPath}"), "OutputPathFailed", ErrorCategory.InvalidArgument, newPath));
            return;
        }

        try
        {
            SubtitleExportHelper.ExtractSubtitle(
                ExecutableService,
                stream,
                mediaFile.Path,
                resolvedOutput,
                WindowsExecutablePathHelper.GetMkvextractPath());
            WriteVerbose($"Extracted {Path.GetFileName(resolvedOutput)}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract subtitle stream {Index} from {Path}", stream.Index, mediaFile.Path);
            WriteError(new ErrorRecord(ex, "SubtitleExportFailed", ErrorCategory.OperationStopped, mediaFile.Path));
        }
    }
}
