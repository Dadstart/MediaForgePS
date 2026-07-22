using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitleOcrRepairWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public SubtitleOcrRepairWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_OcrWorkflow_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Run_WhenOcrRequiredButConverterUnavailable_ReturnsNullAndWritesError()
    {
        var io = new FakeCmdletIO();
        var converter = new Mock<IImageSubtitleOcrConverter>();
        converter.SetupGet(c => c.IsSupportedOnCurrentPlatform).Returns(true);
        converter.SetupGet(c => c.IsAvailable).Returns(false);
        converter.SetupGet(c => c.ExpectedTessDataDescription).Returns("tessdata expected");
        var pathResolver = new Mock<IPathResolver>();

        var imagePath = Path.Combine(_tempDir, "movie.sup");
        File.WriteAllBytes(imagePath, Array.Empty<byte>());

        var result = SubtitleOcrRepairWorkflow.Run(
            io,
            NullLogger.Instance,
            converter.Object,
            pathResolver.Object,
            [imagePath],
            Array.Empty<string>(),
            performOcr: true,
            throttleLimit: 1,
            shouldRepair: false,
            backupPath: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Single(io.Errors);
        Assert.Contains("TesseractDataNotFound", io.Errors[0].FullyQualifiedErrorId, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WhenOcrRequiredButPlatformUnsupported_ReturnsNullAndWritesWarning()
    {
        var io = new FakeCmdletIO();
        var converter = new Mock<IImageSubtitleOcrConverter>();
        converter.SetupGet(c => c.IsSupportedOnCurrentPlatform).Returns(false);
        converter.SetupGet(c => c.IsAvailable).Returns(false);
        converter.SetupGet(c => c.ExpectedTessDataDescription).Returns("OCR is Windows only.");
        var pathResolver = new Mock<IPathResolver>();

        var imagePath = Path.Combine(_tempDir, "movie.sup");
        File.WriteAllBytes(imagePath, Array.Empty<byte>());

        var result = SubtitleOcrRepairWorkflow.Run(
            io,
            NullLogger.Instance,
            converter.Object,
            pathResolver.Object,
            [imagePath],
            Array.Empty<string>(),
            performOcr: true,
            throttleLimit: 1,
            shouldRepair: false,
            backupPath: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Contains("OCR is Windows only.", Assert.Single(io.Warnings), StringComparison.Ordinal);
        Assert.Single(io.Errors);
        Assert.Contains("ImageSubtitleOcrUnsupportedPlatform", io.Errors[0].FullyQualifiedErrorId, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WhenNoOcrNeeded_ReturnsExistingSrtPaths()
    {
        var io = new FakeCmdletIO();
        var converter = new Mock<IImageSubtitleOcrConverter>();
        converter.SetupGet(c => c.IsSupportedOnCurrentPlatform).Returns(true);
        converter.SetupGet(c => c.IsAvailable).Returns(true);
        var pathResolver = new Mock<IPathResolver>();
        var srtPath = Path.Combine(_tempDir, "movie.srt");
        File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\nHi\n");

        var result = SubtitleOcrRepairWorkflow.Run(
            io,
            NullLogger.Instance,
            converter.Object,
            pathResolver.Object,
            Array.Empty<string>(),
            [srtPath],
            performOcr: false,
            throttleLimit: 1,
            shouldRepair: false,
            backupPath: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.ConvertedSrtPaths);
        Assert.Equal(srtPath, Assert.Single(result.AllSrtPaths));
        converter.Verify(c => c.ConvertToSrt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Run_WhenOcrSucceeds_ReturnsConvertedPathsAndDeletesSourceByDefault()
    {
        var io = new FakeCmdletIO();
        var converter = new Mock<IImageSubtitleOcrConverter>();
        converter.SetupGet(c => c.IsSupportedOnCurrentPlatform).Returns(true);
        converter.SetupGet(c => c.IsAvailable).Returns(true);
        converter
            .Setup(c => c.ConvertToSrt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, output, _) =>
                File.WriteAllText(output, "1\n00:00:00,000 --> 00:00:01,000\nHi\n"));

        var pathResolver = new Mock<IPathResolver>();
        var supPath = Path.Combine(_tempDir, "movie.sup");
        File.WriteAllBytes(supPath, Array.Empty<byte>());
        var expectedSrt = Path.ChangeExtension(supPath, "srt")!;

        var result = SubtitleOcrRepairWorkflow.Run(
            io,
            NullLogger.Instance,
            converter.Object,
            pathResolver.Object,
            [supPath],
            Array.Empty<string>(),
            performOcr: true,
            throttleLimit: 1,
            shouldRepair: false,
            backupPath: null,
            cancellationToken: TestContext.Current.CancellationToken,
            keepSource: false);

        Assert.NotNull(result);
        Assert.Equal(expectedSrt, Assert.Single(result.ConvertedSrtPaths));
        Assert.True(File.Exists(expectedSrt));
        Assert.False(File.Exists(supPath));
        Assert.Empty(io.Errors);
    }
}
