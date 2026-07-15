using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
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
    /// The stream index is included when more than one subtitle shares the same extension, or when an image-based
    /// track (SUP/SUB) would OCR to the same .srt path as a lone unindexed text subtitle in the same file.
    /// </summary>
    public static string GetOutputPath(
        string mediaFilePath,
        int streamIndex,
        int sameExtensionCount,
        string extension,
        int englishSubtitleCount)
    {
        var basePath = Path.ChangeExtension(mediaFilePath, null)?.TrimEnd('.') ?? mediaFilePath;
        var includeStreamIndex = sameExtensionCount > 1
            || (englishSubtitleCount > 1 && IsImageBasedExportExtension(extension));
        return includeStreamIndex
            ? basePath + $".{streamIndex}.eng.sdh.{extension}"
            : basePath + $".eng.sdh.{extension}";
    }

    private static bool IsImageBasedExportExtension(string extension) =>
        extension.Equals("sup", StringComparison.OrdinalIgnoreCase)
        || extension.Equals("sub", StringComparison.OrdinalIgnoreCase);

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
    /// </summary>
    public static IReadOnlyDictionary<string, int> BuildExtensionCounts(IEnumerable<MediaStream> subtitles)
    {
        return subtitles
            .GroupBy(GetExtensionForStream, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the English subtitle streams in a media file. Centralizes the filter so all subtitle
    /// export paths agree on what "English" means (codec_type == subtitle and language starts with "en").
    /// </summary>
    public static IReadOnlyList<MediaStream> GetEnglishSubtitleStreams(MediaFile media)
    {
        return (media.Streams ?? Array.Empty<MediaStream>())
            .Where(IsEnglishSubtitle)
            .ToList();
    }

    /// <summary>
    /// Whether the given stream is an English subtitle stream (codec_type == subtitle and language starts with "en").
    /// </summary>
    public static bool IsEnglishSubtitle(MediaStream stream) =>
        string.Equals(stream.Type, "subtitle", StringComparison.OrdinalIgnoreCase)
        && (stream.Language ?? string.Empty).StartsWith("en", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Per-stream plan produced by <see cref="ExtractEnglishSubtitles"/> when iterating extractable subtitle streams.
    /// </summary>
    public readonly record struct SubtitleExportPlan(
        MediaStream Stream,
        string Extension,
        int SameExtensionCount,
        int EnglishSubtitleCount,
        bool IsKnownCodec);

    /// <summary>
    /// Iterates the English subtitle streams in a media file and extracts each via <see cref="ExtractSubtitle"/>.
    /// Callers supply <paramref name="buildOutputPath"/> to compute the candidate output path for each plan
    /// (typically by calling <see cref="GetOutputPath"/> with a caller-chosen base path) and optionally
    /// <paramref name="finalizeOutputPath"/> to resolve/redirect or skip (return <c>null</c>) the path.
    /// </summary>
    /// <param name="executableService">Used to invoke ffmpeg or mkvextract.</param>
    /// <param name="media">Source media file.</param>
    /// <param name="mkvextractPath">Path to mkvextract.exe (required for VobSub streams in Matroska sources).</param>
    /// <param name="buildOutputPath">Maps a plan to a candidate output file path.</param>
    /// <param name="finalizeOutputPath">Optional path post-processor; return null to skip the stream.</param>
    /// <param name="onUnknownCodec">Invoked once per stream whose codec is not in <see cref="CodecToExtension"/>.</param>
    /// <param name="onExtractFailed">Invoked when extraction of a stream throws.</param>
    /// <param name="onNoEnglishSubtitles">Invoked when the file has no English subtitle streams.</param>
    /// <param name="logger">Optional logger used for failure logging.</param>
    /// <returns>Paths of successfully extracted subtitle streams.</returns>
    public static IReadOnlyList<string> ExtractEnglishSubtitles(
        IExecutableService executableService,
        MediaFile media,
        string? mkvextractPath,
        Func<SubtitleExportPlan, string> buildOutputPath,
        Func<string, string?>? finalizeOutputPath = null,
        Action<MediaStream>? onUnknownCodec = null,
        Action<MediaStream, Exception>? onExtractFailed = null,
        Action? onNoEnglishSubtitles = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executableService);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(buildOutputPath);

        var subtitles = GetEnglishSubtitleStreams(media);
        if (subtitles.Count == 0)
        {
            onNoEnglishSubtitles?.Invoke();
            return Array.Empty<string>();
        }

        var englishSubtitleCount = subtitles.Count;
        var extensionCounts = BuildExtensionCounts(subtitles);
        var results = new List<string>(englishSubtitleCount);

        foreach (var stream in subtitles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isKnown = CodecToExtension.TryGetValue(stream.Codec ?? string.Empty, out var ext);
            ext ??= "bin";
            if (!isKnown)
                onUnknownCodec?.Invoke(stream);

            var sameExtensionCount = extensionCounts.TryGetValue(ext, out var count) ? count : 1;
            var plan = new SubtitleExportPlan(stream, ext, sameExtensionCount, englishSubtitleCount, isKnown);

            var candidatePath = buildOutputPath(plan);
            var finalPath = finalizeOutputPath == null ? candidatePath : finalizeOutputPath(candidatePath);
            if (finalPath == null)
                continue;

            try
            {
                ExtractSubtitle(executableService, stream, media.Path, finalPath, mkvextractPath, cancellationToken);
                results.Add(finalPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to extract subtitle stream {Index} from {Path}", stream.Index, media.Path);
                onExtractFailed?.Invoke(stream, ex);
            }
        }

        return results;
    }

    /// <summary>
    /// Resolves path or MediaFile inputs to a sequence of MediaFile instances (expands directories to .mkv files).
    /// </summary>
    public static IEnumerable<MediaFile> ResolveMediaFiles(
        IEnumerable<object> pathOrMediaFiles,
        ICmdletPathContext paths,
        IMediaReaderService mediaReaderService,
        ILogger logger,
        Action<ErrorRecord> writeError,
        CancellationToken cancellationToken = default)
    {
        var filePaths = new List<string>();
        foreach (var item in pathOrMediaFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                var resolved = paths.GetResolvedProviderPaths(path);
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
            cancellationToken.ThrowIfCancellationRequested();

            MediaFile? mf = null;
            try
            {
                mf = mediaReaderService.GetMediaFileAsync(filePath, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
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
        string? mkvextractPath,
        CancellationToken cancellationToken = default)
    {
        var isMatroskaSource = string.Equals(Path.GetExtension(mediaFilePath), ".mkv", StringComparison.OrdinalIgnoreCase);
        var isVobSub = string.Equals(stream.Codec, "dvd_subtitle", StringComparison.OrdinalIgnoreCase);

        if (isMatroskaSource && isVobSub)
        {
            if (string.IsNullOrEmpty(mkvextractPath))
                throw new FileNotFoundException("mkvextract.exe not found. Install mkvtoolnix or use a different subtitle codec.");

            var args = new[] { "tracks", mediaFilePath, $"{stream.Index}:{resolvedOutputPath}" };
            var mkvResult = executableService.ExecuteAsync(mkvextractPath, args, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
            mkvResult.EnsureProcessSuccess("mkvextract");
            return;
        }

        // For VobSub from non-Matroska containers, target the .idx companion path so Ffmpeg's vobsub
        // muxer writes both the .idx and .sub files alongside each other (matching mkvextract output).
        var ffmpegOutputPath = isVobSub
            ? Path.ChangeExtension(resolvedOutputPath, ".idx")
            : resolvedOutputPath;

        var ffmpegArgs = new List<string> { "-i", mediaFilePath, "-map", $"0:{stream.Index}", "-c", "copy", "-y", ffmpegOutputPath };
        var ffmpegResult = executableService.ExecuteAsync("ffmpeg", ffmpegArgs, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        ffmpegResult.EnsureProcessSuccess("FFmpeg subtitle extract");
    }
}
