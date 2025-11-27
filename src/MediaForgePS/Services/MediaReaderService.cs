using System.Management.Automation;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

public class MediaReaderService : IMediaReaderService
{
    private readonly IFfprobeService _ffprobeService;
    private readonly IMediaModelParser _mediaModelParser;
    private readonly ILogger<MediaReaderService> _logger;

    public MediaReaderService(IFfprobeService ffprobeService, IMediaModelParser mediaModelParser, ILogger<MediaReaderService> logger)
    {
        _ffprobeService = ffprobeService;
        _mediaModelParser = mediaModelParser;
        _logger = logger;
    }

    public async Task<MediaFile?> GetMediaFileAsync(string path)
    {
        _logger.LogInformation("Reading media file: {Path}", path);

        var ffprobeArguments = new[] { "-show_format", "-show_chapters", "-show_streams" };
        _logger.LogDebug("Using ffprobe arguments: {Arguments}", string.Join(", ", ffprobeArguments));

        var result = await _ffprobeService.Execute(path, ffprobeArguments).ConfigureAwait(false);
        if (!result.Success)
        {
            _logger.LogWarning("Failed to retrieve media file information for: {Path}", path);
            return null;
        }

        _logger.LogDebug("Parsing media file information for: {Path}", path);
        var mediaFile = _mediaModelParser.ParseFile(path, result.Json);

        if (mediaFile is not null)
        {
            _logger.LogInformation(
                "Successfully read media file: {Path}. Format: {Format}, Streams: {StreamCount}, Chapters: {ChapterCount}",
                path,
                mediaFile.Format?.Format,
                mediaFile.Streams?.Length ?? 0,
                mediaFile.Chapters?.Length ?? 0);
        }
        else
        {
            _logger.LogWarning("Media file parsing returned null for: {Path}", path);
        }

        return mediaFile;
    }
}