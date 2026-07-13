using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
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

    public string NewProcessingDirectory(ICmdletIO io, string path, string description)
    {
        _logger.LogDebug("Creating {Description} directory: {Path}", description, path);
        var resolvedPath = ResolveAbsolutePath(io, path);
        Directory.CreateDirectory(resolvedPath);
        return resolvedPath;
    }

    public ProcessingDirectoryStructure NewProcessingDirectoryStructure(
        ICmdletIO io,
        string title,
        int season,
        IReadOnlyList<string>? subDirectories = null,
        string? basePath = null)
    {
        return InvokeWithErrorHandling(
            io,
            "Directory structure creation",
            new ProcessingDirectoryStructure(string.Empty, string.Empty, Array.Empty<string>()),
            () =>
            {
                var currentBasePath = string.IsNullOrWhiteSpace(basePath)
                    ? io.Paths.CurrentLocationPath
                    : ResolveAbsolutePath(io, basePath);

                var rootDir = NewProcessingDirectory(io, Path.Combine(currentBasePath, title), "show");
                var seasonDir = NewProcessingDirectory(io, Path.Combine(rootDir, $"Season {season:D2}"), "season");

                var dirs = subDirectories ?? _defaultSubDirectories;
                var createdSubDirs = dirs
                    .Select(subDir => NewProcessingDirectory(io, Path.Combine(seasonDir, subDir), subDir))
                    .ToList();

                return new ProcessingDirectoryStructure(rootDir, seasonDir, createdSubDirs);
            });
    }

    public IReadOnlyList<TvDbEpisodeInfo> InvokeSeasonScan(ICmdletIO io, int season, string? tvDbSeriesUrl, string? tvDbSeasonUrl)
    {
        return InvokeWithErrorHandling(
            io,
            "Season scanning",
            Array.Empty<TvDbEpisodeInfo>(),
            () => _seasonScanPhase.Run(io, season, tvDbSeriesUrl, tvDbSeasonUrl));
    }

    public IReadOnlyList<string> GetFilteredVideoFiles(ICmdletIO io, IReadOnlyList<string> paths, IReadOnlyList<string> filePatterns, long minimumFileSizeBytes)
    {
        return InvokeWithErrorHandling(
            io,
            "File filtering",
            Array.Empty<string>(),
            () => _videoCopyPhase.GetFilteredVideoFiles(io, paths, filePatterns, minimumFileSizeBytes));
    }

    public IReadOnlyList<string> InvokeVideoCopy(ICmdletIO io, VideoCopyRequest request)
    {
        return InvokeWithErrorHandling(
            io,
            "Video copy",
            Array.Empty<string>(),
            () =>
            {
                var normalizedPatterns = NormalizeFilePatterns(request.FilePatterns);
                var normalizedRequest = request with { FilePatterns = normalizedPatterns };
                return _videoCopyPhase.CopyVideoFilesWithMetadata(io, normalizedRequest, BuildEpisodeFileName);
            });
    }

    public ProcessingPhaseStats InvokeChapterExtractionPhase(
        ICmdletIO io,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        int chapterNumber = 3,
        int chapterDurationSeconds = 15,
        string chapterDirectory = "Chapters",
        CancellationToken cancellationToken = default)
    {
        return InvokeWithErrorHandling(
            io,
            "Chapter extraction phase",
            new ProcessingPhaseStats(0, 0, 0),
            () => _chapterExtractionPhase.Run(
                io,
                seasonDir,
                copiedFiles,
                chapterNumber,
                chapterDurationSeconds,
                chapterDirectory,
                (path, description) => NewProcessingDirectory(io, path, description),
                cancellationToken));
    }

    public CaptionExtractionPhaseResult InvokeCaptionExtractionPhase(
        ICmdletIO io,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        string captionDirectory = "Captions",
        CancellationToken cancellationToken = default)
    {
        return InvokeWithErrorHandling(
            io,
            "Caption extraction phase",
            new CaptionExtractionPhaseResult(0, 0, 0, Array.Empty<string>()),
            () => _captionExtractionPhase.Run(
                io,
                seasonDir,
                copiedFiles,
                captionDirectory,
                (path, description) => NewProcessingDirectory(io, path, description),
                cancellationToken));
    }

    public static string BuildEpisodeFileName(string title, int season, TvDbEpisodeInfo episode, string extension) =>
        $"{title} {{tvdb {episode.Id}}} - s{season:D2}e{episode.EpisodeNumber:D2}{extension}";

    private static string ResolveAbsolutePath(ICmdletIO io, string path) =>
        PathHelper.ResolveAbsolutePath(path, io.Paths.CurrentLocationPath);

    private T InvokeWithErrorHandling<T>(ICmdletIO io, string operationName, T defaultValue, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PipelineStoppedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationName} failed", operationName);
            io.WriteError(new ErrorRecord(ex, $"{operationName}Failed", ErrorCategory.OperationStopped, null));
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
