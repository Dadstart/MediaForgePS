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

                var createdSubDirs = new List<string>();
                var dirs = subDirectories ?? _defaultSubDirectories;
                foreach (var subDir in dirs)
                    createdSubDirs.Add(NewProcessingDirectory(cmdlet, Path.Combine(seasonDir, subDir), subDir));

                return new ProcessingDirectoryStructure(rootDir, seasonDir, createdSubDirs);
            });
    }

    public IReadOnlyList<TvDbEpisodeInfo> InvokeSeasonScan(PSCmdlet cmdlet, int season, string? tvDbSeriesUrl, string? tvDbSeasonUrl)
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "Season scanning",
            Array.Empty<TvDbEpisodeInfo>(),
            () =>
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
                    !tvDbSeriesUrl.StartsWith("https://thetvdb.com/series/", StringComparison.OrdinalIgnoreCase))
                {
                    cmdlet.WriteError(new ErrorRecord(
                        new ArgumentException("Invalid TVDb URL format. Expected: https://thetvdb.com/series/show-name"),
                        "InvalidTvDbUrl",
                        ErrorCategory.InvalidArgument,
                        tvDbSeriesUrl));
                    return Array.Empty<TvDbEpisodeInfo>();
                }

                var episodes = new List<TvDbEpisodeInfo>();
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

                foreach (var result in results)
                {
                    var item = result?.BaseObject ?? result;
                    if (item == null)
                        continue;

                    var id = PSObject.AsPSObject(item).Properties["Id"]?.Value?.ToString() ?? string.Empty;
                    var title = PSObject.AsPSObject(item).Properties["Title"]?.Value?.ToString() ?? string.Empty;
                    var episodeNumberRaw = PSObject.AsPSObject(item).Properties["EpisodeNumber"]?.Value?.ToString() ?? "0";
                    var seasonNumberRaw = PSObject.AsPSObject(item).Properties["SeasonNumber"]?.Value?.ToString() ?? season.ToString(CultureInfo.InvariantCulture);

                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    if (!int.TryParse(episodeNumberRaw, out var episodeNumber))
                        continue;
                    if (!int.TryParse(seasonNumberRaw, out var seasonNumber))
                        seasonNumber = season;

                    episodes.Add(new TvDbEpisodeInfo(id, seasonNumber, title, episodeNumber));
                }

                return episodes.ToArray();
            });
    }

    public IReadOnlyList<string> GetFilteredVideoFiles(PSCmdlet cmdlet, IReadOnlyList<string> paths, IReadOnlyList<string> filePatterns, long minimumFileSizeBytes)
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "File filtering",
            Array.Empty<string>(),
            () =>
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

                            var info = new FileInfo(file);
                            if (info.Length > minimumFileSizeBytes)
                                acceptedFiles.Add(info.FullName);
                        }
                    }
                }

                return acceptedFiles.ToArray();
            });
    }

    public IReadOnlyList<string> InvokeVideoCopy(PSCmdlet cmdlet, VideoCopyRequest request)
    {
        return InvokeWithErrorHandling<string[]>(
            cmdlet,
            "Video copy",
            Array.Empty<string>(),
            () =>
            {
                var copiedFiles = new List<string>();
                var normalizedPatterns = NormalizeFilePatterns(request.FilePatterns);
                var acceptedFiles = GetFilteredVideoFiles(cmdlet, request.Paths, normalizedPatterns, request.MinimumFileSizeBytes);
                if (acceptedFiles.Count == 0)
                    return Array.Empty<string>();

                var sortedEpisodes = request.Episodes.OrderBy(e => e.EpisodeNumber).ToList();
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
                    var extension = Path.GetExtension(inputFile);
                    var destinationName = BuildEpisodeFileName(request.Title, request.Season, episode, extension);
                    var destinationPath = Path.Combine(request.Destination, destinationName);

                    Directory.CreateDirectory(request.Destination);
                    File.Copy(inputFile, destinationPath, true);
                    copiedFiles.Add(destinationPath);
                }

                return copiedFiles.ToArray();
            });
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
            () =>
            {
                var chapterDir = NewProcessingDirectory(cmdlet, Path.Combine(seasonDir, chapterDirectory), "chapter");
                var processed = 0;
                var failed = 0;

                foreach (var file in copiedFiles)
                {
                    try
                    {
                        var media = _mediaReaderService.GetMediaFileAsync(file, CancellationToken.None)
                            .ConfigureAwait(false).GetAwaiter().GetResult();
                        if (media == null || media.Chapters.Length < chapterNumber)
                        {
                            failed++;
                            continue;
                        }

                        var chapter = media.Chapters[chapterNumber - 1];
                        var startTime = TimeSpan.FromSeconds((double)chapter.StartTime);
                        var chapterClipPath = Path.Combine(
                            chapterDir,
                            $"{Path.GetFileNameWithoutExtension(file)}.chapter{chapterNumber:D2}.mp4");

                        var arguments = new[]
                        {
                            "-ss", startTime.ToString("c", CultureInfo.InvariantCulture),
                            "-i", file,
                            "-t", chapterDurationSeconds.ToString(CultureInfo.InvariantCulture),
                            "-c", "copy",
                            "-y", chapterClipPath
                        };

                        var result = _executableService.ExecuteAsync("ffmpeg", arguments, CancellationToken.None)
                            .ConfigureAwait(false).GetAwaiter().GetResult();
                        if (result.ExitCode == 0)
                            processed++;
                        else
                            failed++;
                    }
                    catch
                    {
                        failed++;
                    }
                }

                return new ProcessingPhaseStats(processed, failed, copiedFiles.Count);
            });
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
            () =>
            {
                var captionDir = NewProcessingDirectory(cmdlet, Path.Combine(seasonDir, captionDirectory), "caption");
                var processed = 0;
                var failed = 0;

                foreach (var file in copiedFiles)
                {
                    try
                    {
                        var media = _mediaReaderService.GetMediaFileAsync(file, CancellationToken.None)
                            .ConfigureAwait(false).GetAwaiter().GetResult();
                        if (media == null)
                        {
                            failed++;
                            continue;
                        }

                        var subtitles = media.Streams
                            .Where(s => string.Equals(s.Type, "subtitle", StringComparison.OrdinalIgnoreCase))
                            .Where(s => (s.Language ?? string.Empty).StartsWith("en", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (subtitles.Count == 0)
                        {
                            failed++;
                            continue;
                        }

                        var anyStreamExtracted = false;
                        foreach (var stream in subtitles)
                        {
                            var extension = _subtitleCodecExtensions.TryGetValue(stream.Codec ?? string.Empty, out var ext) ? ext : "bin";
                            var fileBaseName = Path.GetFileNameWithoutExtension(file);
                            var outputName = subtitles.Count > 1
                                ? $"{fileBaseName}.{stream.Index}.en.sdh.{extension}"
                                : $"{fileBaseName}.en.sdh.{extension}";
                            var outputPath = Path.Combine(captionDir, outputName);

                            var arguments = new[]
                            {
                                "-i", file,
                                "-map", $"0:{stream.Index}",
                                "-c", "copy",
                                "-y", outputPath
                            };

                            var result = _executableService.ExecuteAsync("ffmpeg", arguments, CancellationToken.None)
                                .ConfigureAwait(false).GetAwaiter().GetResult();
                            if (result.ExitCode == 0)
                                anyStreamExtracted = true;
                        }

                        if (anyStreamExtracted)
                            processed++;
                        else
                            failed++;
                    }
                    catch
                    {
                        failed++;
                    }
                }

                return new ProcessingPhaseStats(processed, failed, copiedFiles.Count);
            });
    }

    public static string BuildEpisodeFileName(string title, int season, TvDbEpisodeInfo episode, string extension)
    {
        return $"{title} {{tvdb {episode.Id}}} - s{season:D2}e{episode.EpisodeNumber:D2}{extension}";
    }

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
}
