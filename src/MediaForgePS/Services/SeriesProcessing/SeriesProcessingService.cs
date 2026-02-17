using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text.RegularExpressions;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

public class SeriesProcessingService : ISeriesProcessingService
{
    private const string TvDbSeriesUrlPrefix = "https://thetvdb.com/series/";
    private const string TvDbSeasonPathSegment = "/seasons/";

    private static readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "MediaForgePS/1.0" } }
    };

    private static readonly Regex _episodeIdRegex = new(@"/series/[^/]+/episodes/(\d+)", RegexOptions.Compiled);

    private static readonly string[] _defaultSubDirectories = ["HandBrake", "Remux", "Topaz", "Bonus"];
    private static readonly Dictionary<string, string> _subtitleCodecExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["subrip"] = "srt",
        ["ass"] = "srt",
        ["ssa"] = "srt",
        ["webvtt"] = "vtt",
        ["dvd_subtitle"] = "sub",
        ["hdmv_pgs_subtitle"] = "sup"
    };

    private readonly ILogger<SeriesProcessingService> _logger;
    private readonly IMediaReaderService _mediaReaderService;
    private readonly IExecutableService _executableService;

    public SeriesProcessingService(
        ILogger<SeriesProcessingService> logger,
        IMediaReaderService mediaReaderService,
        IExecutableService executableService)
    {
        _logger = logger;
        _mediaReaderService = mediaReaderService;
        _executableService = executableService;
    }

    public IReadOnlyList<string> NormalizeFilePatterns(IEnumerable<string> filePatterns)
    {
        var normalized = new List<string>();
        foreach (var pattern in filePatterns.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var current = pattern.Trim();
            if (!current.StartsWith('*'))
                current = $"*{current}";
            if (!current.EndsWith('*'))
                current = $"{current}*";
            normalized.Add(current);
        }

        return normalized;
    }

    public string NewProcessingDirectory(PSCmdlet cmdlet, string path, string description)
    {
        _logger.LogDebug("Creating {Description} directory: {Path}", description, path);
        var resolvedPath = ResolveAbsolutePath(cmdlet, path);
        Directory.CreateDirectory(resolvedPath);
        return resolvedPath;
    }

    public ProcessingDirectoryStructure NewProcessingDirectoryStructure(
        PSCmdlet cmdlet,
        string title,
        int season,
        IReadOnlyList<string>? subDirectories = null,
        string? basePath = null)
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "Directory structure creation",
            new ProcessingDirectoryStructure(string.Empty, string.Empty, Array.Empty<string>()),
            () =>
            {
                var currentBasePath = string.IsNullOrWhiteSpace(basePath)
                    ? cmdlet.SessionState.Path.CurrentLocation.Path
                    : ResolveAbsolutePath(cmdlet, basePath);

                var rootDir = NewProcessingDirectory(cmdlet, Path.Combine(currentBasePath, title), "show");
                var seasonDir = NewProcessingDirectory(cmdlet, Path.Combine(rootDir, $"Season {season:D2}"), "season");

                var dirs = subDirectories ?? _defaultSubDirectories;
                var createdSubDirs = dirs
                    .Select(subDir => NewProcessingDirectory(cmdlet, Path.Combine(seasonDir, subDir), subDir))
                    .ToList();

                return new ProcessingDirectoryStructure(rootDir, seasonDir, createdSubDirs);
            });
    }

    public IReadOnlyList<TvDbEpisodeInfo> InvokeSeasonScan(PSCmdlet cmdlet, int season, string? tvDbSeriesUrl, string? tvDbSeasonUrl)
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "Season scanning",
            Array.Empty<TvDbEpisodeInfo>(),
            () => RunSeasonScan(cmdlet, season, tvDbSeriesUrl, tvDbSeasonUrl));
    }

    public IReadOnlyList<string> GetFilteredVideoFiles(PSCmdlet cmdlet, IReadOnlyList<string> paths, IReadOnlyList<string> filePatterns, long minimumFileSizeBytes)
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "File filtering",
            Array.Empty<string>(),
            () => CollectFilteredVideoFiles(cmdlet, paths, filePatterns, minimumFileSizeBytes));
    }

    public IReadOnlyList<string> InvokeVideoCopy(PSCmdlet cmdlet, VideoCopyRequest request)
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "Video copy",
            Array.Empty<string>(),
            () => CopyVideoFilesWithMetadata(cmdlet, request));
    }

    public ProcessingPhaseStats InvokeChapterExtractionPhase(
        PSCmdlet cmdlet,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        int chapterNumber = 3,
        int chapterDurationSeconds = 15,
        string chapterDirectory = "Chapters")
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "Chapter extraction phase",
            new ProcessingPhaseStats(0, 0, 0),
            () => RunChapterExtraction(cmdlet, seasonDir, copiedFiles, chapterNumber, chapterDurationSeconds, chapterDirectory));
    }

    public ProcessingPhaseStats InvokeCaptionExtractionPhase(
        PSCmdlet cmdlet,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        string captionDirectory = "Captions")
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "Caption extraction phase",
            new ProcessingPhaseStats(0, 0, 0),
            () => RunCaptionExtraction(cmdlet, seasonDir, copiedFiles, captionDirectory));
    }

    public static string BuildEpisodeFileName(string title, int season, TvDbEpisodeInfo episode, string extension) =>
        $"{title} {{tvdb {episode.Id}}} - s{season:D2}e{episode.EpisodeNumber:D2}{extension}";

    private static string ResolveAbsolutePath(PSCmdlet cmdlet, string path)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        var currentLocation = cmdlet.SessionState.Path.CurrentLocation.Path;
        return Path.GetFullPath(Path.Combine(currentLocation, path));
    }

    private static IReadOnlyList<string> ResolveDirectories(PSCmdlet cmdlet, IReadOnlyList<string> paths)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            try
            {
                var resolved = cmdlet.GetResolvedProviderPathFromPSPath(path, out _);
                foreach (var item in resolved)
                {
                    if (Directory.Exists(item))
                        directories.Add(item);
                }
            }
            catch
            {
                if (Directory.Exists(path))
                    directories.Add(Path.GetFullPath(path));
            }
        }

        return directories.ToList();
    }

    private T InvokeWithErrorHandling<T>(PSCmdlet cmdlet, string operationName, T defaultValue, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationName} failed", operationName);
            cmdlet.WriteError(new ErrorRecord(ex, $"{operationName}Failed", ErrorCategory.OperationStopped, null));
            return defaultValue;
        }
    }

    private TvDbEpisodeInfo[] RunSeasonScan(PSCmdlet cmdlet, int season, string? tvDbSeriesUrl, string? tvDbSeasonUrl)
    {
        if (string.IsNullOrWhiteSpace(tvDbSeasonUrl) && string.IsNullOrWhiteSpace(tvDbSeriesUrl))
        {
            cmdlet.WriteError(new ErrorRecord(
                new ArgumentException("Either TvDbSeriesUrl or TvDbSeasonUrl must be provided."),
                "TvDbUrlMissing",
                ErrorCategory.InvalidArgument,
                null));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        if (!string.IsNullOrWhiteSpace(tvDbSeriesUrl) &&
            !tvDbSeriesUrl.StartsWith(TvDbSeriesUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            cmdlet.WriteError(new ErrorRecord(
                new ArgumentException($"Invalid TVDb URL format. Expected: {TvDbSeriesUrlPrefix}show-name"),
                "InvalidTvDbUrl",
                ErrorCategory.InvalidArgument,
                tvDbSeriesUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        if (!string.IsNullOrWhiteSpace(tvDbSeasonUrl) &&
            (!tvDbSeasonUrl.StartsWith(TvDbSeriesUrlPrefix, StringComparison.OrdinalIgnoreCase) ||
             !tvDbSeasonUrl.Contains(TvDbSeasonPathSegment, StringComparison.Ordinal)))
        {
            cmdlet.WriteError(new ErrorRecord(
                new ArgumentException($"Invalid TVDb season URL format. Expected: {TvDbSeriesUrlPrefix}show-name/seasons/..."),
                "InvalidTvDbSeasonUrl",
                ErrorCategory.InvalidArgument,
                tvDbSeasonUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        var seasonUrl = !string.IsNullOrWhiteSpace(tvDbSeasonUrl)
            ? tvDbSeasonUrl
            : $"{tvDbSeriesUrl!.TrimEnd('/')}/seasons/official/{season}";

        _logger.LogDebug("Fetching TVDb season page: {SeasonUrl}", seasonUrl);

        string html;
        try
        {
            html = _httpClient.GetStringAsync(seasonUrl).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            cmdlet.WriteError(new ErrorRecord(ex, "TvDbRequestFailed", ErrorCategory.ConnectionError, seasonUrl));
            return Array.Empty<TvDbEpisodeInfo>();
        }

        var episodeIds = _episodeIdRegex.Matches(html)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(id => int.Parse(id, CultureInfo.InvariantCulture))
            .ToList();

        if (episodeIds.Count == 0)
        {
            _logger.LogDebug("No episode IDs found on the season page");
            return Array.Empty<TvDbEpisodeInfo>();
        }

        var episodes = new List<TvDbEpisodeInfo>(episodeIds.Count);
        for (var i = 0; i < episodeIds.Count; i++)
        {
            var episodeId = episodeIds[i];
            var titlePattern = $"episodes/{Regex.Escape(episodeId)}[^>]*>([^<]+)</a>";
            var titleMatch = Regex.Match(html, titlePattern);
            var title = titleMatch.Success
                ? titleMatch.Groups[1].Value.Trim()
                : $"Episode {i + 1}";
            episodes.Add(new TvDbEpisodeInfo(episodeId, season, title, i + 1));
        }

        _logger.LogDebug("Found {Count} episodes for season {Season}", episodes.Count, season);
        return episodes.ToArray();
    }

    private static bool TryParseTvDbEpisode(PSObject ps, int defaultSeason, out TvDbEpisodeInfo episode)
    {
        episode = null!;
        var id = ps.Properties["Id"]?.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var title = ps.Properties["Title"]?.Value?.ToString() ?? string.Empty;
        var episodeNumberRaw = ps.Properties["EpisodeNumber"]?.Value?.ToString() ?? "0";
        var seasonNumberRaw = ps.Properties["SeasonNumber"]?.Value?.ToString() ?? defaultSeason.ToString(CultureInfo.InvariantCulture);

        if (!int.TryParse(episodeNumberRaw, out var episodeNumber))
            return false;

        var seasonNumber = int.TryParse(seasonNumberRaw, out var sn) ? sn : defaultSeason;
        episode = new TvDbEpisodeInfo(id, seasonNumber, title, episodeNumber);
        return true;
    }

    private string[] CollectFilteredVideoFiles(PSCmdlet cmdlet, IReadOnlyList<string> paths, IReadOnlyList<string> filePatterns, long minimumFileSizeBytes)
    {
        var resolvedDirectories = ResolveDirectories(cmdlet, paths);
        if (resolvedDirectories.Count == 0)
            return Array.Empty<string>();

        var acceptedFiles = new List<string>();
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in resolvedDirectories)
        {
            foreach (var pattern in filePatterns)
            {
                foreach (var file in Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly))
                {
                    if (!seenFiles.Add(file))
                        continue;

                    if (new FileInfo(file).Length > minimumFileSizeBytes)
                        acceptedFiles.Add(file);
                }
            }
        }

        return acceptedFiles.ToArray();
    }

    private const int MainActivityId = 0;
    private const int CurrentItemActivityId = 1;

    private string[] CopyVideoFilesWithMetadata(PSCmdlet cmdlet, VideoCopyRequest request)
    {
        var normalizedPatterns = NormalizeFilePatterns(request.FilePatterns);
        var acceptedFiles = GetFilteredVideoFiles(cmdlet, request.Paths, normalizedPatterns, request.MinimumFileSizeBytes);
        if (acceptedFiles.Count == 0)
            return Array.Empty<string>();

        var filesWithSize = new List<(string Path, long Size)>();
        long totalBytes = 0;
        foreach (var path in acceptedFiles)
        {
            long size = 0;
            try
            {
                var fi = new FileInfo(path);
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

            filesWithSize.Add((path, size));
        }

        var copiedFiles = new List<string>();
        var sortedEpisodes = request.Episodes.OrderBy(e => e.EpisodeNumber).ToList();
        Directory.CreateDirectory(request.Destination);

        var copyStats = new List<FileCopyStats>();
        long completedBytes = 0;

        for (var fileIndex = 0; fileIndex < filesWithSize.Count; fileIndex++)
        {
            var (inputFile, fileSize) = filesWithSize[fileIndex];
            var episodeIndex = (request.EpisodeStart - 1) + fileIndex;
            if (episodeIndex >= sortedEpisodes.Count)
            {
                cmdlet.WriteWarning($"No TVDb episode metadata available for file '{inputFile}'.");
                break;
            }

            var episode = sortedEpisodes[episodeIndex];
            var destinationName = BuildEpisodeFileName(request.Title, request.Season, episode, Path.GetExtension(inputFile));
            var destinationPath = Path.Combine(request.Destination, destinationName);

            var currentFileIndex = fileIndex + 1;
            var percent = totalBytes > 0 ? (int)((completedBytes * 100.0) / totalBytes) : 0;
            var eta = CalculateCopyRemainingTime(completedBytes, totalBytes, copyStats);
            WriteVideoCopyProgress(cmdlet, currentFileIndex, filesWithSize.Count, Path.GetFileName(inputFile), totalBytes, completedBytes, eta, ProgressRecordType.Processing);
            WriteCurrentFileProgress(cmdlet, destinationName, "Copying...", ProgressRecordType.Processing);

            var stopwatch = Stopwatch.StartNew();
            File.Copy(inputFile, destinationPath, true);
            stopwatch.Stop();

            copiedFiles.Add(destinationPath);
            completedBytes += fileSize;
            copyStats.Add(new FileCopyStats { FileSizeBytes = fileSize, ProcessingTime = stopwatch.Elapsed });

            percent = totalBytes > 0 ? (int)((completedBytes * 100.0) / totalBytes) : 100;
            WriteVideoCopyProgress(cmdlet, currentFileIndex, filesWithSize.Count, Path.GetFileName(inputFile), totalBytes, completedBytes, null, ProgressRecordType.Processing);
            WriteCurrentFileProgress(cmdlet, destinationName, "Completed", ProgressRecordType.Completed);
        }

        WriteProgressCompleted(cmdlet, "Video copy", "Current file");

        return copiedFiles.ToArray();
    }

    private static void WriteVideoCopyProgress(
        PSCmdlet cmdlet,
        int currentFileIndex,
        int totalFiles,
        string currentFileName,
        long totalBytes,
        long completedBytes,
        TimeSpan? eta,
        ProgressRecordType recordType)
    {
        var status = $"File {currentFileIndex} of {totalFiles}";
        var percent = totalBytes > 0 ? (int)((completedBytes * 100.0) / totalBytes) : 0;
        status += $" ({percent}%)";
        if (totalBytes > 0)
        {
            status += $" — {FormatByteCount(completedBytes)} / {FormatByteCount(totalBytes)}";
        }

        status += $" — {currentFileName}";

        var progressRecord = MediaConversionHelper.CreateSimpleProgressRecord(
            MainActivityId,
            "Video copy",
            status,
            percent,
            recordType: recordType);
        if (eta.HasValue)
            progressRecord.StatusDescription = $"ETA: {FormatTimespan(eta.Value)}";
        cmdlet.WriteProgress(progressRecord);
    }

    private static void WriteCurrentFileProgress(
        PSCmdlet cmdlet,
        string currentOperation,
        string status,
        ProgressRecordType recordType)
    {
        var progressRecord = MediaConversionHelper.CreateNestedProgressRecord(
            CurrentItemActivityId,
            "Current file",
            status,
            MainActivityId,
            currentOperation,
            recordType: recordType);
        cmdlet.WriteProgress(progressRecord);
    }

    private static void WriteProgressCompleted(PSCmdlet cmdlet, string mainActivity, string currentActivity)
    {
        cmdlet.WriteProgress(MediaConversionHelper.CreateSimpleProgressRecord(
            MainActivityId,
            mainActivity,
            "Completed",
            recordType: ProgressRecordType.Completed));
        cmdlet.WriteProgress(MediaConversionHelper.CreateSimpleProgressRecord(
            CurrentItemActivityId,
            currentActivity,
            "Completed",
            recordType: ProgressRecordType.Completed));
    }

    private static TimeSpan? CalculateCopyRemainingTime(long completedBytes, long totalBytes, List<FileCopyStats> stats)
    {
        if (stats.Count == 0)
            return null;

        var averageBytesPerSecond = stats.Average(s => s.BytesPerSecond);
        if (averageBytesPerSecond <= 0)
            return null;

        var remainingBytes = totalBytes - completedBytes;
        if (remainingBytes <= 0)
            return null;

        return TimeSpan.FromSeconds(remainingBytes / averageBytesPerSecond);
    }

    private static string FormatByteCount(long bytes)
    {
        if (bytes >= 1 << 30)
            return $"{bytes / (double)(1 << 30):F1} GB";
        if (bytes >= 1 << 20)
            return $"{bytes / (double)(1 << 20):F1} MB";
        if (bytes >= 1 << 10)
            return $"{bytes / (double)(1 << 10):F1} KB";
        return $"{bytes} B";
    }

    private static string FormatTimespan(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{time.Hours}h {time.Minutes}m {time.Seconds}s";
        if (time.TotalMinutes >= 1)
            return $"{time.Minutes}m {time.Seconds}s";
        return $"{time.Seconds}s";
    }

    private sealed class FileCopyStats
    {
        public long FileSizeBytes { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public double BytesPerSecond => FileSizeBytes > 0 && ProcessingTime.TotalSeconds > 0
            ? FileSizeBytes / ProcessingTime.TotalSeconds
            : 0;
    }

    private ProcessingPhaseStats RunChapterExtraction(
        PSCmdlet cmdlet,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        int chapterNumber,
        int chapterDurationSeconds,
        string chapterDirectory)
    {
        var chapterDir = NewProcessingDirectory(cmdlet, Path.Combine(seasonDir, chapterDirectory), "chapter");
        var processed = 0;
        var failed = 0;
        var total = copiedFiles.Count;

        for (var i = 0; i < copiedFiles.Count; i++)
        {
            var file = copiedFiles[i];
            var current = i + 1;
            var percent = total > 0 ? (int)((i * 100.0) / total) : 0;
            WritePhaseProgress(cmdlet, "Chapter extraction", current, total, Path.GetFileName(file), percent, ProgressRecordType.Processing);
            WriteCurrentFileProgress(cmdlet, Path.GetFileName(file), "Extracting chapter...", ProgressRecordType.Processing);

            if (TryExtractChapterClip(file, chapterDir, chapterNumber, chapterDurationSeconds))
                processed++;
            else
                failed++;

            percent = total > 0 ? (int)((current * 100.0) / total) : 100;
            WritePhaseProgress(cmdlet, "Chapter extraction", current, total, Path.GetFileName(file), percent, ProgressRecordType.Processing);
            WriteCurrentFileProgress(cmdlet, Path.GetFileName(file), "Completed", ProgressRecordType.Completed);
        }

        WriteProgressCompleted(cmdlet, "Chapter extraction", "Current file");
        return new ProcessingPhaseStats(processed, failed, copiedFiles.Count);
    }

    private bool TryExtractChapterClip(string filePath, string chapterDir, int chapterNumber, int chapterDurationSeconds)
    {
        try
        {
            var media = _mediaReaderService.GetMediaFileAsync(filePath, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (media == null || media.Chapters.Length < chapterNumber)
                return false;

            var chapter = media.Chapters[chapterNumber - 1];
            var startTime = TimeSpan.FromSeconds((double)chapter.StartTime);
            var clipPath = Path.Combine(chapterDir, $"{Path.GetFileNameWithoutExtension(filePath)}.chapter{chapterNumber:D2}.mp4");

            var arguments = new[]
            {
                "-ss", startTime.ToString("c", CultureInfo.InvariantCulture),
                "-i", filePath,
                "-t", chapterDurationSeconds.ToString(CultureInfo.InvariantCulture),
                "-c", "copy",
                "-y", clipPath
            };

            var result = _executableService.ExecuteAsync("ffmpeg", arguments, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private ProcessingPhaseStats RunCaptionExtraction(
        PSCmdlet cmdlet,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        string captionDirectory)
    {
        var captionDir = NewProcessingDirectory(cmdlet, Path.Combine(seasonDir, captionDirectory), "caption");
        var processed = 0;
        var failed = 0;
        var total = copiedFiles.Count;

        for (var i = 0; i < copiedFiles.Count; i++)
        {
            var file = copiedFiles[i];
            var current = i + 1;
            var percent = total > 0 ? (int)((i * 100.0) / total) : 0;
            WritePhaseProgress(cmdlet, "Caption extraction", current, total, Path.GetFileName(file), percent, ProgressRecordType.Processing);
            WriteCurrentFileProgress(cmdlet, Path.GetFileName(file), "Extracting captions...", ProgressRecordType.Processing);

            if (TryExtractCaptions(file, captionDir))
                processed++;
            else
                failed++;

            percent = total > 0 ? (int)((current * 100.0) / total) : 100;
            WritePhaseProgress(cmdlet, "Caption extraction", current, total, Path.GetFileName(file), percent, ProgressRecordType.Processing);
            WriteCurrentFileProgress(cmdlet, Path.GetFileName(file), "Completed", ProgressRecordType.Completed);
        }

        WriteProgressCompleted(cmdlet, "Caption extraction", "Current file");
        return new ProcessingPhaseStats(processed, failed, copiedFiles.Count);
    }

    private static void WritePhaseProgress(
        PSCmdlet cmdlet,
        string activity,
        int currentFileIndex,
        int totalFiles,
        string currentFileName,
        int percentComplete,
        ProgressRecordType recordType)
    {
        var status = $"File {currentFileIndex} of {totalFiles} ({percentComplete}%) — {currentFileName}";
        var progressRecord = MediaConversionHelper.CreateSimpleProgressRecord(
            MainActivityId,
            activity,
            status,
            percentComplete,
            recordType: recordType);
        cmdlet.WriteProgress(progressRecord);
    }

    private bool TryExtractCaptions(string filePath, string captionDir)
    {
        try
        {
            var media = _mediaReaderService.GetMediaFileAsync(filePath, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (media == null)
                return false;

            var subtitles = media.Streams
                .Where(s => string.Equals(s.Type, "subtitle", StringComparison.OrdinalIgnoreCase))
                .Where(s => (s.Language ?? string.Empty).StartsWith("en", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (subtitles.Count == 0)
                return false;

            var anyExtracted = false;
            var fileBaseName = Path.GetFileNameWithoutExtension(filePath);

            foreach (var stream in subtitles)
            {
                var ext = _subtitleCodecExtensions.TryGetValue(stream.Codec ?? string.Empty, out var e) ? e : "bin";
                var outputName = subtitles.Count > 1
                    ? $"{fileBaseName}.{stream.Index}.en.sdh.{ext}"
                    : $"{fileBaseName}.en.sdh.{ext}";
                var outputPath = Path.Combine(captionDir, outputName);

                var arguments = new[] { "-i", filePath, "-map", $"0:{stream.Index}", "-c", "copy", "-y", outputPath };
                var result = _executableService.ExecuteAsync("ffmpeg", arguments, CancellationToken.None)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (result.ExitCode == 0)
                    anyExtracted = true;
            }

            return anyExtracted;
        }
        catch
        {
            return false;
        }
    }
}
