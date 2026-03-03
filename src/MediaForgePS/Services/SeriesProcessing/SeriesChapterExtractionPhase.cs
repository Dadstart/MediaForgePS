using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

internal sealed class SeriesChapterExtractionPhase(
    IMediaReaderService mediaReaderService,
    IExecutableService executableService)
{
    public ProcessingPhaseStats Run(
        PSCmdlet cmdlet,
        string seasonDir,
        IReadOnlyList<string> copiedFiles,
        int chapterNumber,
        int chapterDurationSeconds,
        string chapterDirectory,
        Func<string, string, string> createDirectory)
    {
        var chapterDir = createDirectory(Path.Combine(seasonDir, chapterDirectory), "chapter");
        var processed = 0;
        var failed = 0;
        var total = copiedFiles.Count;

        for (var i = 0; i < copiedFiles.Count; i++)
        {
            var file = copiedFiles[i];
            var current = i + 1;
            var fileName = Path.GetFileName(file);
            var (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, fileName);
            MediaConversionHelper.WriteMainProgress(cmdlet, "Chapter extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(cmdlet, "Current file", "Extracting chapter...", fileName, recordType: ProgressRecordType.Processing);

            if (TryExtractChapterClip(file, chapterDir, chapterNumber, chapterDurationSeconds))
                processed++;
            else
                failed++;

            (phaseStatus, percent) = MediaConversionHelper.BuildCountBasedProgressStatus(current, total, fileName);
            MediaConversionHelper.WriteMainProgress(cmdlet, "Chapter extraction", phaseStatus, percent, recordType: ProgressRecordType.Processing);
            MediaConversionHelper.WriteCurrentItemProgress(cmdlet, "Current file", "Completed", fileName, recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(cmdlet, "Chapter extraction", "Current file");
        return new ProcessingPhaseStats(processed, failed, copiedFiles.Count);
    }

    private bool TryExtractChapterClip(string filePath, string chapterDir, int chapterNumber, int chapterDurationSeconds)
    {
        try
        {
            var media = mediaReaderService.GetMediaFileAsync(filePath, CancellationToken.None)
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

            var result = executableService.ExecuteAsync("ffmpeg", arguments, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
