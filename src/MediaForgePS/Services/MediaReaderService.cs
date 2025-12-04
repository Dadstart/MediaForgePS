using System.Management.Automation;
using System.Text.Json;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Microsoft.Extensions.Logging;

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

    public async Task<MediaFile?> GetMediaFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _logger.LogInformation("Reading media file: {Path}", path);

        var ffprobeArguments = new[] { "-show_format", "-show_chapters", "-show_streams" };
        _logger.LogDebug("Using ffprobe arguments: {Arguments}", string.Join(", ", ffprobeArguments));

        var result = await _ffprobeService.ExecuteAsync(path, ffprobeArguments, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            _logger.LogWarning("Failed to retrieve media file information for: {Path}", path);
            return null;
        }

        _logger.LogDebug("Parsing media file information for: {Path}", path);
        var mediaFile = _mediaModelParser.ParseFile(path, result.Json);

        _logger.LogInformation(
            "Successfully read media file: {Path}. Format: {Format}, Streams: {StreamCount}, Chapters: {ChapterCount}",
            path,
            mediaFile.Format?.Format,
            mediaFile.Streams?.Length ?? 0,
            mediaFile.Chapters?.Length ?? 0);

        return mediaFile;
    }
}
