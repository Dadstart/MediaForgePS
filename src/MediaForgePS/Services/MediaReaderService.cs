using System.Management.Automation;
using System.Text.Json;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

public class MediaReaderService : IMediaReaderService
{
    private readonly IFfprobeService _ffprobeService;
    private readonly IMediaModelParser _mediaModelParser;

    public MediaReaderService(IFfprobeService ffprobeService, IMediaModelParser mediaModelParser)
    {
        _ffprobeService = ffprobeService;
        _mediaModelParser = mediaModelParser;
    }

    public async Task<MediaFile?> GetMediaFile(string path)
    {
        var result = await _ffprobeService.Execute(path, new[] { "-show_format", "-show_streams" });
        if (!result.Success)
            return null;

        var mediaFile = _mediaModelParser.ParseFile(path, result.Json);
        return mediaFile;
    }
}