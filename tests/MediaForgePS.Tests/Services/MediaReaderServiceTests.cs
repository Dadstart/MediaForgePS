using System.Linq;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class MediaReaderServiceTests
{
    private readonly Mock<IFfprobeService> _ffprobeServiceMock;
    private readonly Mock<IMediaModelParser> _parserMock;
    private readonly Mock<ILogger<MediaReaderService>> _loggerMock;
    private readonly MediaReaderService _mediaReaderService;

    public MediaReaderServiceTests()
    {
        _ffprobeServiceMock = new Mock<IFfprobeService>();
        _parserMock = new Mock<IMediaModelParser>();
        _loggerMock = new Mock<ILogger<MediaReaderService>>();
        _mediaReaderService = new MediaReaderService(_ffprobeServiceMock.Object, _parserMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetMediaFileAsync_WithSuccess_ReturnsMediaFile()
    {
        // Arrange
        var path = "test.mkv";
        var json = "{\"format\": {}, \"streams\": [], \"chapters\": []}";
        var ffprobeResult = new FfprobeResult(true, json);
        var expectedMediaFile = new MediaFile(path, new MediaFormat(null, path, 0, "matroska", "Matroska", 0, 0, 0, 0, new Dictionary<string, string>(), json), Array.Empty<MediaChapter>(), Array.Empty<MediaStream>(), json);

        _ffprobeServiceMock.Setup(s => s.Execute(path, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(ffprobeResult);
        _parserMock.Setup(p => p.ParseFile(path, json))
            .Returns(expectedMediaFile);

        // Act
        var result = await _mediaReaderService.GetMediaFileAsync(path);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMediaFile, result);
    }

    [Fact]
    public async Task GetMediaFileAsync_WithFfprobeFailure_ReturnsNull()
    {
        // Arrange
        var path = "test.mkv";
        var ffprobeResult = new FfprobeResult(false, string.Empty);

        _ffprobeServiceMock.Setup(s => s.Execute(path, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(ffprobeResult);

        // Act
        var result = await _mediaReaderService.GetMediaFileAsync(path);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMediaFileAsync_UsesCorrectFfprobeArguments()
    {
        // Arrange
        var path = "test.mkv";
        var json = "{\"format\": {}, \"streams\": [], \"chapters\": []}";
        var ffprobeResult = new FfprobeResult(true, json);
        var expectedMediaFile = new MediaFile(path, new MediaFormat(null, path, 0, "matroska", "Matroska", 0, 0, 0, 0, new Dictionary<string, string>(), json), Array.Empty<MediaChapter>(), Array.Empty<MediaStream>(), json);

        _ffprobeServiceMock.Setup(s => s.Execute(path, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(ffprobeResult);
        _parserMock.Setup(p => p.ParseFile(path, json))
            .Returns(expectedMediaFile);

        // Act
        await _mediaReaderService.GetMediaFileAsync(path);

        // Assert
        _ffprobeServiceMock.Verify(s => s.Execute(
            path,
            It.Is<IEnumerable<string>>(args => args.Contains("-show_format") && args.Contains("-show_chapters") && args.Contains("-show_streams"))),
            Times.Once);
    }

    [Fact]
    public async Task GetMediaFileAsync_WithParserReturningNull_ReturnsNull()
    {
        // Arrange
        var path = "test.mkv";
        var json = "{\"format\": {}, \"streams\": [], \"chapters\": []}";
        var ffprobeResult = new FfprobeResult(true, json);

        _ffprobeServiceMock.Setup(s => s.Execute(path, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(ffprobeResult);
        _parserMock.Setup(p => p.ParseFile(path, json))
            .Returns((MediaFile?)null);

        // Act
        var result = await _mediaReaderService.GetMediaFileAsync(path);

        // Assert
        Assert.Null(result);
    }
}
