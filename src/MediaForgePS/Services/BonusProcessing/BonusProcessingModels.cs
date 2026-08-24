using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services.BonusProcessing;

public record BonusConversionRequest(
    string InputDirectory,
    string DefaultVideoEncoder,
    bool Force);

public record BonusConversionPhaseResult(
    IReadOnlyList<MediaConversionResult> Results,
    int DiscoveredFileCount);

public record BonusCaptionExtractionRequest(
    string InputDirectory);

public record BonusOrganizationRequest(
    string SourceDirectory,
    string DestinationDirectory);

public record BonusOrganizationPhaseResult(
    int FilesMoved,
    int MoveCandidates);
