using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

public interface ISeriesProcessingService
{
    IReadOnlyList<string> NormalizeFilePatterns(IEnumerable<string> filePatterns);
    string NewProcessingDirectory(PSCmdlet cmdlet, string path, string description);
    ProcessingDirectoryStructure NewProcessingDirectoryStructure(PSCmdlet cmdlet, string title, int season, IReadOnlyList<string>? subDirectories = null, string? basePath = null);
    IReadOnlyList<TvDbEpisodeInfo> InvokeSeasonScan(PSCmdlet cmdlet, int season, string? tvDbSeriesUrl, string? tvDbSeasonUrl);
    IReadOnlyList<string> GetFilteredVideoFiles(PSCmdlet cmdlet, IReadOnlyList<string> paths, IReadOnlyList<string> filePatterns, long minimumFileSizeBytes);
    IReadOnlyList<string> InvokeVideoCopy(PSCmdlet cmdlet, VideoCopyRequest request);
    ProcessingPhaseStats InvokeChapterExtractionPhase(PSCmdlet cmdlet, string seasonDir, IReadOnlyList<string> copiedFiles, int chapterNumber = 3, int chapterDurationSeconds = 15, string chapterDirectory = "Chapters", CancellationToken cancellationToken = default);
    CaptionExtractionPhaseResult InvokeCaptionExtractionPhase(PSCmdlet cmdlet, string seasonDir, IReadOnlyList<string> copiedFiles, string captionDirectory = "Captions", CancellationToken cancellationToken = default);
}
