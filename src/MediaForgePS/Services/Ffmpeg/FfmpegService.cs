using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

public class FfmpegService : IFfmpegService
{
    private readonly IExecutableService _executableService;

    public FfmpegService(IExecutableService executableService)
    {
        _executableService = executableService;
    }
}