using System;
using System.Collections.Generic;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;

namespace Dadstart.Labs.MediaForge.Services.BonusProcessing;

public interface IBonusProcessingService
{
    IReadOnlyList<(string FolderName, string Suffix)> PlexLayout { get; }

    IReadOnlyList<string> GetBonusMkvPaths(string inputDirectory);

    BonusConversionPhaseResult InvokeConversionPhase(
        ICmdletIO io,
        BonusConversionRequest request,
        Action<MediaConversionResult>? emitResult = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> InvokeCaptionExtractionPhase(
        ICmdletIO io,
        BonusCaptionExtractionRequest request,
        CancellationToken cancellationToken = default);

    BonusOrganizationPhaseResult InvokeOrganizationPhase(
        ICmdletIO io,
        BonusOrganizationRequest request,
        CancellationToken cancellationToken = default);
}
