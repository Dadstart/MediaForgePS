using System.Collections.Generic;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

public interface ISeriesProcessingService
{
    IReadOnlyList<string> NormalizeFilePatterns(IEnumerable<string> filePatterns);
    string NewProcessingDirectory(ICmdletIO io, string path, string description);
    ProcessingDirectoryStructure NewProcessingDirectoryStructure(ICmdletIO io, string title, int season, IReadOnlyList<string>? subDirectories = null, string? basePath = null);
    IReadOnlyList<TvDbEpisodeInfo> InvokeSeasonScan(ICmdletIO io, int season, string? tvDbSeriesUrl, string? tvDbSeasonUrl);
    IReadOnlyList<string> GetFilteredVideoFiles(ICmdletIO io, IReadOnlyList<string> paths, IReadOnlyList<string> filePatterns, long minimumFileSizeBytes);
    IReadOnlyList<string> InvokeVideoCopy(ICmdletIO io, VideoCopyRequest request);
    ProcessingPhaseStats InvokeChapterExtractionPhase(ICmdletIO io, string seasonDir, IReadOnlyList<string> copiedFiles, int chapterNumber = 3, int chapterDurationSeconds = 15, string chapterDirectory = "Chapters", CancellationToken cancellationToken = default);
    CaptionExtractionPhaseResult InvokeCaptionExtractionPhase(ICmdletIO io, string seasonDir, IReadOnlyList<string> copiedFiles, string captionDirectory = "Captions", CancellationToken cancellationToken = default);
}
