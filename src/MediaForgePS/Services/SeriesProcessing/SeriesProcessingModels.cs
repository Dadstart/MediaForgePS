using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services.SeriesProcessing;

public record ProcessingDirectoryStructure(
    string RootDir,
    string SeasonDir,
    IReadOnlyList<string> SubDirs);

public record ProcessingPhaseStats(
    int Processed,
    int Failed,
    int Total);

public record VideoCopyRequest(
    IReadOnlyList<string> Paths,
    string Destination,
    string Title,
    int Season,
    IReadOnlyList<TvDbEpisodeInfo> Episodes,
    IReadOnlyList<string> FilePatterns,
    int EpisodeStart,
    long MinimumFileSizeBytes);
