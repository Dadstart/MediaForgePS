using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Service for executing Ffmpeg operations. Currently a placeholder for future Ffmpeg functionality.
/// </summary>
public class FfmpegService(IExecutableService executableService) : IFfmpegService
{
    private readonly IExecutableService _executableService = executableService;
}
