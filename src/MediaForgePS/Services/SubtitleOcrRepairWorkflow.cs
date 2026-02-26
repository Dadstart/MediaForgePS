using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Shared OCR and SRT repair workflow used by subtitle cmdlets.
/// </summary>
public static class SubtitleOcrRepairWorkflow
{
    /// <summary>
    /// Runs optional OCR conversion and optional SRT repair, writing output paths through <paramref name="writeObject"/>.
    /// Returns null when workflow cannot continue (for example, Subtitle Edit missing when OCR is required).
    /// </summary>
    public static IReadOnlyList<string>? Run(
        PSCmdlet cmdlet,
        ILogger logger,
        IExecutableService executableService,
        IPathResolver pathResolver,
        IReadOnlyList<string> imagePaths,
        IReadOnlyList<string> srtPaths,
        bool performOcr,
        int throttleLimit,
        bool shouldRepair,
        string? backupPath,
        Action<string> writeObject)
    {
        IReadOnlyList<string> convertedSrtPaths = Array.Empty<string>();
        if (performOcr && imagePaths.Count > 0)
        {
            var subtitleEditPath = WindowsExecutablePathHelper.GetSubtitleEditPath();
            if (string.IsNullOrEmpty(subtitleEditPath))
            {
                cmdlet.WriteError(new ErrorRecord(
                    new FileNotFoundException($"Subtitle Edit not found (required to convert {imagePaths.Count} image subtitle(s)). Expected: {WindowsExecutablePathHelper.GetSubtitleEditExpectedPath()}"),
                    "SubtitleEditNotFound",
                    ErrorCategory.ObjectNotFound,
                    null));
                return null;
            }

            convertedSrtPaths = ImageSubtitleConversionHelper.ConvertImagePathsToSrtParallel(
                cmdlet,
                executableService,
                logger,
                subtitleEditPath,
                imagePaths,
                Math.Max(1, throttleLimit),
                cmdlet.WriteError);
        }

        var allSrtPaths = srtPaths.Concat(convertedSrtPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allSrtPaths.Count > 0)
            SrtRepairHelper.RunRepairLoop(cmdlet, logger, pathResolver, allSrtPaths, shouldRepair, backupPath, writeObject);

        return allSrtPaths;
    }
}
