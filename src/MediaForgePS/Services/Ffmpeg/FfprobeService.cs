using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

public class FfprobeService : IFfprobeService
{
    private const string FFPROBE_EXECUTABLE = "ffprobe";
    private readonly IExecutableService _executableService;

    public FfprobeService(IExecutableService executableService)
    {
        _executableService = executableService;
    }

    public string Execute(string path, string arguments)
    {
        return _executableService.Execute(FFPROBE_EXECUTABLE, arguments);
    }
}