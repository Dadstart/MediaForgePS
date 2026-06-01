using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

internal sealed class SeriesCaptionExtractionPhase(
    IMediaReaderService mediaReaderService,
    IExecutableService executableService,
    ILogger logger)
{
    public CaptionExtractionPhaseResult Run(
        PSCmdlet cmdlet,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        string captionDirectory,
        Func<string, string, string> createDirectory)
    {
        var captionDir = createDirectory(Path.Combine(seasonDir, captionDirectory), "caption");
        var processed = 0;
        var failed = 0;
        var total = copiedFiles.Count;
        var extractedCaptionPaths = new List<string>();

        for (var i = 0; i < copiedFiles.Count; i++)
        {
            var file = copiedFiles[i];
            var current = i + 1;
            var fileName = Path.GetFileName(file);
            var (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, fileName);
            MediaConversionHelper.WriteMainProgress(cmdlet, "Caption extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(cmdlet, "Current file", "Extracting captions...", fileName, recordType: ProgressRecordType.Processing);

            var extractedFromFile = TryExtractCaptions(file, captionDir);
            if (extractedFromFile.Count > 0)
            {
                processed++;
                extractedCaptionPaths.AddRange(extractedFromFile);
            }
            else
                failed++;

            (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, fileName);
            MediaConversionHelper.WriteMainProgress(cmdlet, "Caption extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(cmdlet, "Current file", "Completed", fileName, recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(cmdlet, "Caption extraction", "Current file");
        return new CaptionExtractionPhaseResult(processed, failed, copiedFiles.Count, extractedCaptionPaths);
    }

    private IReadOnlyList<string> TryExtractCaptions(string filePath, string captionDir)
    {
        try
        {
            var media = mediaReaderService.GetMediaFileAsync(filePath, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (media == null)
                return Array.Empty<string>();

            var mkvextractPath = WindowsExecutablePathHelper.GetMkvextractPath();

            return SubtitleExportHelper.ExtractEnglishSubtitles(
                executableService,
                media,
                mkvextractPath,
                buildOutputPath: plan =>
                {
                    var sameNamingPath = SubtitleExportHelper.GetOutputPath(
                        filePath, plan.Stream.Index, plan.SameExtensionCount, plan.Extension);
                    return Path.Combine(captionDir, Path.GetFileName(sameNamingPath));
                },
                logger: logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract captions from {Path}", filePath);
            return Array.Empty<string>();
        }
    }
}
