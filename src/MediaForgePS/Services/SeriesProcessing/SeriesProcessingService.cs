using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

public class SeriesProcessingService : ISeriesProcessingService
{
    private static readonly string[] _defaultSubDirectories = ["Bonus"];

    private readonly ILogger<SeriesProcessingService> _logger;
    private readonly SeriesSeasonScanPhase _seasonScanPhase;
    private readonly SeriesVideoCopyPhase _videoCopyPhase;
    private readonly SeriesChapterExtractionPhase _chapterExtractionPhase;
    private readonly SeriesCaptionExtractionPhase _captionExtractionPhase;

    public SeriesProcessingService(
        ILogger<SeriesProcessingService> logger,
        IMediaReaderService mediaReaderService,
        IExecutableService executableService)
    {
        _logger = logger;
        _seasonScanPhase = new SeriesSeasonScanPhase(logger);
        _videoCopyPhase = new SeriesVideoCopyPhase();
        _chapterExtractionPhase = new SeriesChapterExtractionPhase(mediaReaderService, executableService);
        _captionExtractionPhase = new SeriesCaptionExtractionPhase(mediaReaderService, executableService, logger);
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
            () => _seasonScanPhase.Run(cmdlet, season, tvDbSeriesUrl, tvDbSeasonUrl));
    }

    public IReadOnlyList<string> GetFilteredVideoFiles(PSCmdlet cmdlet, IReadOnlyList<string> paths, IReadOnlyList<string> filePatterns, long minimumFileSizeBytes)
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "File filtering",
            Array.Empty<string>(),
            () => _videoCopyPhase.GetFilteredVideoFiles(cmdlet, paths, filePatterns, minimumFileSizeBytes));
    }

    public IReadOnlyList<string> InvokeVideoCopy(PSCmdlet cmdlet, VideoCopyRequest request)
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "Video copy",
            Array.Empty<string>(),
            () =>
            {
                var normalizedPatterns = NormalizeFilePatterns(request.FilePatterns);
                var normalizedRequest = request with { FilePatterns = normalizedPatterns };
                return _videoCopyPhase.CopyVideoFilesWithMetadata(cmdlet, normalizedRequest, BuildEpisodeFileName);
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
            () => _chapterExtractionPhase.Run(
                cmdlet,
                seasonDir,
                copiedFiles,
                chapterNumber,
                chapterDurationSeconds,
                chapterDirectory,
                (path, description) => NewProcessingDirectory(cmdlet, path, description)));
    }

    public CaptionExtractionPhaseResult InvokeCaptionExtractionPhase(
        PSCmdlet cmdlet,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        string captionDirectory = "Captions")
    {
        return InvokeWithErrorHandling(
            cmdlet,
            "Caption extraction phase",
            new CaptionExtractionPhaseResult(0, 0, 0, Array.Empty<string>()),
            () => _captionExtractionPhase.Run(
                cmdlet,
                seasonDir,
                copiedFiles,
                captionDirectory,
                (path, description) => NewProcessingDirectory(cmdlet, path, description)));
    }

    public static string BuildEpisodeFileName(string title, int season, TvDbEpisodeInfo episode, string extension) =>
        $"{title} {{tvdb {episode.Id}}} - s{season:D2}e{episode.EpisodeNumber:D2}{extension}";

    private static string ResolveAbsolutePath(PSCmdlet cmdlet, string path) =>
        PathHelper.ResolveAbsolutePath(path, cmdlet.SessionState.Path.CurrentLocation.Path);

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

}
