using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.BonusProcessing;

internal sealed class BonusCaptionExtractionPhase(
    IMediaReaderService mediaReaderService,
    IExecutableService executableService,
    IPathResolver pathResolver,
    ILogger logger)
{
    public IReadOnlyList<string> Run(
        ICmdletIO io,
        BonusCaptionExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var bonusMkvPaths = BonusPlexLayout.GetBonusMkvPaths(request.InputDirectory);
        if (bonusMkvPaths.Count == 0)
            return Array.Empty<string>();

        io.WriteVerbose($"Extracting subtitles from {bonusMkvPaths.Count} bonus file(s)...");
        var exportedPaths = new List<string>();
        var mkvextractPath = WindowsExecutablePathHelper.GetMkvextractPath();

        foreach (var mkvPath in bonusMkvPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(mkvPath);
            MediaConversionHelper.WriteCurrentItemProgress(io, "Subtitle extraction", $"Extracting... - {fileName}", recordType: ProgressRecordType.Processing);

            MediaFile? mediaFile;
            try
            {
                mediaFile = mediaReaderService.GetMediaFileAsync(mkvPath, cancellationToken)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read media file for subtitle extraction: {Path}", mkvPath);
                continue;
            }

            if (mediaFile == null)
                continue;

            var extracted = SubtitleExportHelper.ExtractEnglishSubtitles(
                executableService,
                mediaFile,
                mkvextractPath,
                buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                    mediaFile.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
                finalizeOutputPath: candidate => pathResolver.TryResolveOutputPath(candidate, out var resolved) ? resolved : null,
                onUnknownCodec: stream => io.WriteWarning($"Unknown codec: {stream.Codec} - using .bin extension"),
                onExtractFailed: (_, ex) =>
                {
                    if (ex is PlatformNotSupportedException pns)
                    {
                        io.WriteWarning(pns.Message);
                        return;
                    }

                    io.WriteError(new ErrorRecord(
                        ex,
                        "SubtitleExportFailed",
                        ErrorCategory.OperationStopped,
                        mediaFile.Path));
                },
                onNoEnglishSubtitles: () => io.WriteVerbose($"No English subtitles in {fileName}"),
                logger,
                cancellationToken);

            foreach (var path in extracted)
            {
                io.WriteVerbose($"Extracted {Path.GetFileName(path)}");
                exportedPaths.Add(path);
            }

            MediaConversionHelper.WriteCurrentItemProgress(io, "Subtitle extraction", $"Completed - {fileName}", recordType: ProgressRecordType.Completed);
        }

        MediaConversionHelper.WriteProgressCompleted(io, "Subtitle extraction", "Current file");
        return exportedPaths;
    }
}
