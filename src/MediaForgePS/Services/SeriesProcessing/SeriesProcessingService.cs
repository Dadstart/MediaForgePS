using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

public class SeriesProcessingService : ISeriesProcessingService
{
    private const string TvDbSeriesUrlPrefix = "https://thetvdb.com/series/";

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

        using var ps = PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Get-TvDbEpisodeInfo");
        ps.AddParameter("SeasonNumber", season);
        if (!string.IsNullOrWhiteSpace(tvDbSeasonUrl))
            ps.AddParameter("SeasonUrl", tvDbSeasonUrl);
        else
            ps.AddParameter("SeriesUrl", tvDbSeriesUrl);

        var results = ps.Invoke();
        if (ps.HadErrors)
        {
            foreach (var err in ps.Streams.Error)
                cmdlet.WriteError(err);
            return Array.Empty<TvDbEpisodeInfo>();
        }

        var episodes = new List<TvDbEpisodeInfo>();
        foreach (var result in results)
        {
            var item = result?.BaseObject ?? result;
            if (item == null)
                continue;

            if (TryParseTvDbEpisode(PSObject.AsPSObject(item), season, out var episode))
                episodes.Add(episode);
        }

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

    private string[] CopyVideoFilesWithMetadata(PSCmdlet cmdlet, VideoCopyRequest request)
    {
        var normalizedPatterns = NormalizeFilePatterns(request.FilePatterns);
        var acceptedFiles = GetFilteredVideoFiles(cmdlet, request.Paths, normalizedPatterns, request.MinimumFileSizeBytes);
        if (acceptedFiles.Count == 0)
            return Array.Empty<string>();

        var copiedFiles = new List<string>();
        var sortedEpisodes = request.Episodes.OrderBy(e => e.EpisodeNumber).ToList();
        Directory.CreateDirectory(request.Destination);

        for (var fileIndex = 0; fileIndex < acceptedFiles.Count; fileIndex++)
        {
            var episodeIndex = (request.EpisodeStart - 1) + fileIndex;
            if (episodeIndex >= sortedEpisodes.Count)
            {
                cmdlet.WriteWarning($"No TVDb episode metadata available for file '{acceptedFiles[fileIndex]}'.");
                break;
            }

            var episode = sortedEpisodes[episodeIndex];
            var inputFile = acceptedFiles[fileIndex];
            var destinationName = BuildEpisodeFileName(request.Title, request.Season, episode, Path.GetExtension(inputFile));
            var destinationPath = Path.Combine(request.Destination, destinationName);

            File.Copy(inputFile, destinationPath, true);
            copiedFiles.Add(destinationPath);
        }

        return copiedFiles.ToArray();
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

        foreach (var file in copiedFiles)
        {
            if (TryExtractChapterClip(file, chapterDir, chapterNumber, chapterDurationSeconds))
                processed++;
            else
                failed++;
        }

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

        foreach (var file in copiedFiles)
        {
            if (TryExtractCaptions(file, captionDir))
                processed++;
            else
                failed++;
        }

        return new ProcessingPhaseStats(processed, failed, copiedFiles.Count);
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
