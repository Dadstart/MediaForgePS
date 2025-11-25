using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

public class MediaReaderService : IMediaReaderService
{
    private readonly IFfprobeService _ffprobeService;

    public MediaReaderService(IFfprobeService ffprobeService)
    {
        _ffprobeService = ffprobeService;
    }

    public MediaFile GetMediaFile(string path)
    {
        return _ffprobeService.GetMediaFile(path);
    }
}