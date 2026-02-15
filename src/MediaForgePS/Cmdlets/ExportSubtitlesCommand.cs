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
    private static readonly Dictionary<string, string> _codecToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["subrip"] = "srt",
        ["ass"] = "srt",
        ["ssa"] = "srt",
        ["webvtt"] = "vtt",
        ["dvd_subtitle"] = "sub",
        ["hdmv_pgs_subtitle"] = "sup"
    };

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

    private void WriteHostMessage(string message, ConsoleColor? foregroundColor = null)
    {
        var hostMsg = new HostInformationMessage { Message = message };
        if (foregroundColor.HasValue)
            hostMsg.ForegroundColor = foregroundColor.Value;
        var record = new InformationRecord(hostMsg, "PSHOST");
        WriteInformation(record);
    }

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

        var mediaFiles = ResolveMediaFiles().ToList();
        if (mediaFiles.Count == 0)
        {
            WriteWarning("No media files to process.");
            return;
        }

        WriteHostMessage($"Extracting subtitles ({mediaFiles.Count} file(s))", ConsoleColor.Cyan);
        WriteProgress(new ProgressRecord(0, "Extracting subtitles", $"Processing {mediaFiles.Count} file(s)...")
        {
            PercentComplete = 0,
            RecordType = ProgressRecordType.Processing
        });

        var fileIndex = 0;
        foreach (var mf in mediaFiles)
        {
            fileIndex++;
            var fileName = System.IO.Path.GetFileName(mf.Path);
            WriteProgress(new ProgressRecord(0, "Extracting subtitles", $"{fileName} ({fileIndex}/{mediaFiles.Count})")
            {
                PercentComplete = (int)Math.Round((fileIndex * 100.0) / mediaFiles.Count, 0),
                RecordType = ProgressRecordType.Processing
            });

            ExportSubtitlesForMediaFile(mf, mediaFiles.Count, fileIndex);
        }

        WriteProgress(new ProgressRecord(0, "Extracting subtitles", "Complete") { RecordType = ProgressRecordType.Completed });
    }

    private IEnumerable<MediaFile> ResolveMediaFiles()
    {
        var filePaths = new List<string>();
        foreach (var item in _pathOrMediaFiles)
        {
            var unwrapped = item is PSObject ps ? ps.BaseObject : item;
            if (unwrapped is MediaFile mf)
            {
                yield return mf;
                continue;
            }
            var path = unwrapped?.ToString()?.Trim();
            if (string.IsNullOrEmpty(path))
                continue;
            try
            {
                var resolved = GetResolvedProviderPathFromPSPath(path, out _);
                foreach (var r in resolved)
                {
                    if (File.Exists(r))
                        filePaths.Add(r);
                    else if (Directory.Exists(r))
                        filePaths.AddRange(Directory.GetFiles(r, "*.mkv"));
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Could not resolve path: {Path}", path);
                WriteError(new ErrorRecord(new FileNotFoundException("Path does not exist.", path), "PathNotFound", ErrorCategory.ObjectNotFound, path));
            }
        }

        foreach (var filePath in filePaths)
        {
            MediaFile? mf = null;
            try
            {
                mf = MediaReaderService.GetMediaFileAsync(filePath, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not read media file: {Path}", filePath);
                WriteError(new ErrorRecord(ex, "MediaFileReadFailed", ErrorCategory.ReadError, filePath));
            }
            if (mf != null)
                yield return mf;
        }
    }

    private void ExportSubtitlesForMediaFile(MediaFile mediaFile, int totalFiles, int fileIndex)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(mediaFile.Path);
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
            WriteProgress(new ProgressRecord(1, fileName, $"Stream {sub.Index} ({sub.Codec})")
            {
                PercentComplete = (int)Math.Round((subIndex * 100.0) / subtitles.Count, 0),
                RecordType = ProgressRecordType.Processing
            });
            ExportSingleSubtitle(sub, mediaFile, subIndex, subtitles.Count);
        }

        WriteProgress(new ProgressRecord(1, fileName, "Complete") { RecordType = ProgressRecordType.Completed });
    }

    private void ExportSingleSubtitle(MediaStream stream, MediaFile mediaFile, int subtitleIndex, int totalSubtitleCount)
    {
        if (!_codecToExtension.TryGetValue(stream.Codec ?? "", out var ext))
        {
            WriteWarning($"Unknown codec: {stream.Codec} - using .bin extension");
            ext = "bin";
        }

        var basePath = System.IO.Path.ChangeExtension(mediaFile.Path, null)?.TrimEnd('.') ?? mediaFile.Path;
        var newPath = totalSubtitleCount > 1
            ? basePath + $".{stream.Index}.en.sdh.{ext}"
            : basePath + $".en.sdh.{ext}";

        if (!PathResolver.TryResolveOutputPath(newPath, out var resolvedOutput))
        {
            WriteError(new ErrorRecord(new InvalidOperationException($"Failed to resolve output path: {newPath}"), "OutputPathFailed", ErrorCategory.InvalidArgument, newPath));
            return;
        }

        try
        {
            if (string.Equals(stream.Codec, "dvd_subtitle", StringComparison.OrdinalIgnoreCase))
            {
                var mkvextract = GetMkvextractPath();
                if (mkvextract == null)
                {
                    WriteError(new ErrorRecord(new FileNotFoundException("mkvextract.exe not found. Install mkvtoolnix or use a different subtitle codec."), "MkvextractNotFound", ErrorCategory.ObjectNotFound, null));
                    return;
                }
                var args = new[] { "tracks", mediaFile.Path, $"{stream.Index}:{resolvedOutput}" };
                var result = ExecutableService.ExecuteAsync(mkvextract, args, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                if (result.ExitCode != 0)
                    throw new InvalidOperationException($"mkvextract failed with exit code {result.ExitCode}. {result.ErrorOutput}");
            }
            else
            {
                var ffmpegArgs = new List<string> { "-i", mediaFile.Path, "-map", $"0:{stream.Index}", "-c", "copy", "-y", resolvedOutput };
                var result = ExecutableService.ExecuteAsync("ffmpeg", ffmpegArgs, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                if (result.ExitCode != 0)
                    throw new InvalidOperationException($"FFmpeg failed with exit code {result.ExitCode}. {result.ErrorOutput}");
            }

            WriteVerbose($"Extracted {System.IO.Path.GetFileName(resolvedOutput)}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract subtitle stream {Index} from {Path}", stream.Index, mediaFile.Path);
            WriteError(new ErrorRecord(ex, "SubtitleExportFailed", ErrorCategory.OperationStopped, mediaFile.Path));
        }
    }

    private static string? GetMkvextractPath()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var path = System.IO.Path.Combine(programFiles, "mkvtoolnix", "mkvextract.exe");
        return File.Exists(path) ? path : null;
    }
}
