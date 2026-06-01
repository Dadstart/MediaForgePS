using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Shared logic for exporting subtitle streams from media files (path building, codec mapping, extraction).
/// </summary>
public static class SubtitleExportHelper
{
    /// <summary>
    /// Mapping from ffprobe/FFmpeg subtitle codec names to file extensions for export.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CodecToExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["subrip"] = "srt",
        ["ass"] = "srt",
        ["ssa"] = "srt",
        ["webvtt"] = "vtt",
        ["dvd_subtitle"] = "sub",
        ["hdmv_pgs_subtitle"] = "sup"
    };

    /// <summary>
    /// Builds the output file path for an exported subtitle stream (e.g. movie.eng.sdh.srt or movie.2.eng.sdh.sup).
    /// The stream index is included only when more than one subtitle shares the same extension.
    /// </summary>
    public static string GetOutputPath(string mediaFilePath, int streamIndex, int sameExtensionCount, string extension)
    {
        var basePath = Path.ChangeExtension(mediaFilePath, null)?.TrimEnd('.') ?? mediaFilePath;
        return sameExtensionCount > 1
            ? basePath + $".{streamIndex}.eng.sdh.{extension}"
            : basePath + $".eng.sdh.{extension}";
    }

    /// <summary>
    /// Resolves the output extension for a subtitle stream's codec, falling back to "bin" if unknown.
    /// </summary>
    public static string GetExtensionForStream(MediaStream stream)
    {
        return CodecToExtension.TryGetValue(stream.Codec ?? string.Empty, out var ext)
            ? ext
            : "bin";
    }

    /// <summary>
    /// Builds a case-insensitive map of output extension → number of subtitle streams that will use that extension.
    /// Use the returned count when calling <see cref="GetOutputPath"/> so the stream index is omitted when an
    /// extension is unique within the file.
    /// </summary>
    public static IReadOnlyDictionary<string, int> BuildExtensionCounts(IEnumerable<MediaStream> subtitles)
    {
        return subtitles
            .GroupBy(GetExtensionForStream, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves path or MediaFile inputs to a sequence of MediaFile instances (expands directories to .mkv files).
    /// </summary>
    public static IEnumerable<MediaFile> ResolveMediaFiles(
        IEnumerable<object> pathOrMediaFiles,
        PSCmdlet cmdlet,
        IMediaReaderService mediaReaderService,
        ILogger logger,
        Action<ErrorRecord> writeError)
    {
        var filePaths = new List<string>();
        foreach (var item in pathOrMediaFiles)
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
                var resolved = cmdlet.GetResolvedProviderPathFromPSPath(path, out _);
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
                logger.LogDebug(ex, "Could not resolve path: {Path}", path);
                writeError(new ErrorRecord(new FileNotFoundException("Path does not exist.", path), "PathNotFound", ErrorCategory.ObjectNotFound, path));
            }
        }

        foreach (var filePath in filePaths)
        {
            MediaFile? mf = null;
            try
            {
                mf = mediaReaderService.GetMediaFileAsync(filePath, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read media file: {Path}", filePath);
                writeError(new ErrorRecord(ex, "MediaFileReadFailed", ErrorCategory.ReadError, filePath));
            }
            if (mf != null)
                yield return mf;
        }
    }

    /// <summary>
    /// Extracts a single subtitle stream to a file. Uses mkvextract for VobSub (dvd_subtitle) streams in
    /// Matroska (.mkv) sources because it reliably produces the .idx companion. For all other combinations
    /// (non-Matroska sources or non-VobSub codecs) falls back to Ffmpeg with stream copy. Throws on failure.
    /// </summary>
    public static void ExtractSubtitle(
        IExecutableService executableService,
        MediaStream stream,
        string mediaFilePath,
        string resolvedOutputPath,
        string? mkvextractPath)
    {
        var isMatroskaSource = string.Equals(Path.GetExtension(mediaFilePath), ".mkv", StringComparison.OrdinalIgnoreCase);
        var isVobSub = string.Equals(stream.Codec, "dvd_subtitle", StringComparison.OrdinalIgnoreCase);

        if (isMatroskaSource && isVobSub)
        {
            if (string.IsNullOrEmpty(mkvextractPath))
                throw new FileNotFoundException("mkvextract.exe not found. Install mkvtoolnix or use a different subtitle codec.");

            var args = new[] { "tracks", mediaFilePath, $"{stream.Index}:{resolvedOutputPath}" };
            var mkvResult = executableService.ExecuteAsync(mkvextractPath, args, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            if (mkvResult.ExitCode != 0)
                throw new InvalidOperationException($"mkvextract failed with exit code {mkvResult.ExitCode}. {mkvResult.ErrorOutput}");

            return;
        }

        // For VobSub from non-Matroska containers, target the .idx companion path so Ffmpeg's vobsub
        // muxer writes both the .idx and .sub files alongside each other (matching mkvextract output).
        var ffmpegOutputPath = isVobSub
            ? Path.ChangeExtension(resolvedOutputPath, ".idx")
            : resolvedOutputPath;

        var ffmpegArgs = new List<string> { "-i", mediaFilePath, "-map", $"0:{stream.Index}", "-c", "copy", "-y", ffmpegOutputPath };
        var ffmpegResult = executableService.ExecuteAsync("ffmpeg", ffmpegArgs, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        if (ffmpegResult.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg failed with exit code {ffmpegResult.ExitCode}. {ffmpegResult.ErrorOutput}");
    }
}
