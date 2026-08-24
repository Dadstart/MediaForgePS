using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Parsers;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class MediaReaderServiceTests
{
    private static readonly string[] ExpectedFfprobeArguments = ["-show_format", "-show_chapters", "-show_streams"];

    [Fact]
    public async Task GetMediaFileAsync_WhenFfprobeSucceeds_ReturnsParsedMediaFile()
    {
        const string path = @"C:\media\video.mkv";
        const string json = """{"format":{}}""";
        var expectedMedia = CreateMediaFile(path);
        var ffprobeMock = new Mock<IFfprobeService>();
        var parserMock = new Mock<IMediaModelParser>();

        ffprobeMock.Setup(f => f.ExecuteAsync(path, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FfprobeResult(true, json));
        parserMock.Setup(p => p.ParseFile(path, json)).Returns(expectedMedia);

        var service = new MediaReaderService(ffprobeMock.Object, parserMock.Object, NullLogger<MediaReaderService>.Instance);

        var result = await service.GetMediaFileAsync(path, TestContext.Current.CancellationToken);

        Assert.Same(expectedMedia, result);
        ffprobeMock.Verify(
            f => f.ExecuteAsync(
                path,
                It.Is<IEnumerable<string>>(args => args.SequenceEqual(ExpectedFfprobeArguments)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        parserMock.Verify(p => p.ParseFile(path, json), Times.Once);
    }

    [Fact]
    public async Task GetMediaFileAsync_WhenFfprobeFails_ReturnsNull()
    {
        const string path = @"C:\media\missing.mkv";
        var ffprobeMock = new Mock<IFfprobeService>();
        var parserMock = new Mock<IMediaModelParser>();

        ffprobeMock.Setup(f => f.ExecuteAsync(path, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FfprobeResult(false, string.Empty));

        var service = new MediaReaderService(ffprobeMock.Object, parserMock.Object, NullLogger<MediaReaderService>.Instance);

        var result = await service.GetMediaFileAsync(path, TestContext.Current.CancellationToken);

        Assert.Null(result);
        parserMock.Verify(p => p.ParseFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetMediaFileAsync_WhenPathIsNullOrWhitespace_Throws(string? path)
    {
        var service = new MediaReaderService(
            Mock.Of<IFfprobeService>(),
            Mock.Of<IMediaModelParser>(),
            NullLogger<MediaReaderService>.Instance);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.GetMediaFileAsync(path!, TestContext.Current.CancellationToken));
    }

    private static MediaFile CreateMediaFile(string path) =>
        new(
            path,
            new MediaFormat(path, 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            [],
            []);
}
