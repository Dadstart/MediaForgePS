using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

internal sealed class SeriesChapterExtractionPhase(
    IMediaReaderService mediaReaderService,
    IExecutableService executableService,
    ILogger logger)
{
    public ProcessingPhaseStats Run(
        ICmdletIO io,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        int chapterNumber,
        int chapterDurationSeconds,
        string chapterDirectory,
        Func<string, string, string> createDirectory,
        CancellationToken cancellationToken = default)
    {
        var chapterDir = createDirectory(Path.Combine(seasonDir, chapterDirectory), "chapter");
        var processed = 0;
        var failed = 0;
        var total = copiedFiles.Count;

        for (var i = 0; i < copiedFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = copiedFiles[i];
            var current = i + 1;
            var fileName = Path.GetFileName(file);
            var (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, fileName);
            MediaConversionHelper.WriteMainProgress(io, "Chapter extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(io, "Current file", "Extracting chapter...", fileName, recordType: ProgressRecordType.Processing);

            if (TryExtractChapterClip(io, file, chapterDir, chapterNumber, chapterDurationSeconds, cancellationToken))
                processed++;
            else
                failed++;

            (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, fileName);
            MediaConversionHelper.WriteMainProgress(io, "Chapter extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(io, "Current file", "Completed", fileName, recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(io, "Chapter extraction", "Current file");
        return new ProcessingPhaseStats(processed, failed, copiedFiles.Count);
    }

    private bool TryExtractChapterClip(
        ICmdletErrorSink errors,
        string filePath,
        string chapterDir,
        int chapterNumber,
        int chapterDurationSeconds,
        CancellationToken cancellationToken)
    {
        string? tempDirectory = null;
        try
        {
            var media = mediaReaderService.GetMediaFileAsync(filePath, cancellationToken)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (media == null || media.Chapters.Length < chapterNumber)
            {
                var message = media == null
                    ? $"Unable to read media metadata for chapter extraction: {filePath}"
                    : $"File does not contain chapter {chapterNumber}: {filePath}";
                logger.LogWarning("{Message}", message);
                errors.WriteError(new ErrorRecord(
                    new InvalidOperationException(message),
                    "ChapterExtractionSkipped",
                    ErrorCategory.ObjectNotFound,
                    filePath));
                return false;
            }

            var chapter = media.Chapters[chapterNumber - 1];
            var startTime = TimeSpan.FromSeconds((double)chapter.StartTime);
            var clipPath = Path.Combine(chapterDir, $"{Path.GetFileNameWithoutExtension(filePath)}.chapter{chapterNumber:D2}.mp4");
            var tempClipPath = AtomicFileHelper.CreateTempOutputPath(clipPath);
            tempDirectory = Path.GetDirectoryName(tempClipPath);

            var arguments = new[]
            {
                "-ss", startTime.ToString("c", CultureInfo.InvariantCulture),
                "-i", filePath,
                "-t", chapterDurationSeconds.ToString(CultureInfo.InvariantCulture),
                "-c", "copy",
                "-y", tempClipPath
            };

            var result = executableService.ExecuteAsync("ffmpeg", arguments, cancellationToken)
                .ConfigureAwait(false).GetAwaiter().GetResult();

            result.EnsureProcessSuccess($"ffmpeg chapter extraction for '{filePath}'");
            AtomicFileHelper.PromoteTempFile(tempClipPath, clipPath);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chapter extraction failed for {Path}", filePath);
            errors.WriteError(new ErrorRecord(
                ex,
                "ChapterExtractionFailed",
                ErrorCategory.OperationStopped,
                filePath));
            return false;
        }
        finally
        {
            AtomicFileHelper.TryDeleteDirectory(tempDirectory);
        }
    }
}
