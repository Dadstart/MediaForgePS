using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

public class FfmpegService(IExecutableService executableService) : IFfmpegService
{
    private readonly IExecutableService _executableService = executableService;
}
