using System;
using System.Collections.Generic;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services.BonusProcessing;

public class BonusProcessingService : IBonusProcessingService
{
    private readonly BonusConversionPhase _conversionPhase;
    private readonly BonusCaptionExtractionPhase _captionExtractionPhase;
    private readonly BonusOrganizationPhase _organizationPhase;

    public BonusProcessingService(
        ILogger<BonusProcessingService> logger,
        IMediaReaderService mediaReaderService,
        IMediaConversionService mediaConversionService,
        IExecutableService executableService,
        IPathResolver pathResolver)
    {
        _conversionPhase = new BonusConversionPhase(mediaReaderService, mediaConversionService, logger);
        _captionExtractionPhase = new BonusCaptionExtractionPhase(mediaReaderService, executableService, pathResolver, logger);
        _organizationPhase = new BonusOrganizationPhase(logger);
    }

    public IReadOnlyList<(string FolderName, string Suffix)> PlexLayout => BonusPlexLayout._entries;

    public IReadOnlyList<string> GetBonusMkvPaths(string inputDirectory) =>
        BonusPlexLayout.GetBonusMkvPaths(inputDirectory);

    public BonusConversionPhaseResult InvokeConversionPhase(
        ICmdletIO io,
        BonusConversionRequest request,
        Action<MediaConversionResult>? emitResult = null,
        CancellationToken cancellationToken = default) =>
        _conversionPhase.Run(io, request, emitResult, cancellationToken);

    public IReadOnlyList<string> InvokeCaptionExtractionPhase(
        ICmdletIO io,
        BonusCaptionExtractionRequest request,
        CancellationToken cancellationToken = default) =>
        _captionExtractionPhase.Run(io, request, cancellationToken);

    public BonusOrganizationPhaseResult InvokeOrganizationPhase(
        ICmdletIO io,
        BonusOrganizationRequest request,
        CancellationToken cancellationToken = default) =>
        _organizationPhase.Run(io, request, cancellationToken);
}
