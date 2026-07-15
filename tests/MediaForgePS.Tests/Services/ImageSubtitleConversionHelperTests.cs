using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class ImageSubtitleConversionHelperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IExecutableService> _executableServiceMock = new();

    public ImageSubtitleConversionHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_ImageSubtitleConversion_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void DeleteImageSubtitleSourceFiles_Sup_DeletesSupOnly()
    {
        var supPath = Path.Combine(_tempDir, "movie.eng.sdh.sup");
        File.WriteAllBytes(supPath, Array.Empty<byte>());

        ImageSubtitleConversionHelper.DeleteImageSubtitleSourceFiles(supPath);

        Assert.False(File.Exists(supPath));
    }

    [Fact]
    public void DeleteImageSubtitleSourceFiles_Sub_DeletesSubAndIdxCompanion()
    {
        var subPath = Path.Combine(_tempDir, "movie.eng.sdh.sub");
        var idxPath = Path.Combine(_tempDir, "movie.eng.sdh.idx");
        File.WriteAllBytes(subPath, Array.Empty<byte>());
        File.WriteAllBytes(idxPath, Array.Empty<byte>());

        ImageSubtitleConversionHelper.DeleteImageSubtitleSourceFiles(subPath);

        Assert.False(File.Exists(subPath));
        Assert.False(File.Exists(idxPath));
    }

    [Fact]
    public void DeleteImageSubtitleSourceFiles_Idx_DeletesIdxAndSubCompanion()
    {
        var subPath = Path.Combine(_tempDir, "movie.eng.sdh.sub");
        var idxPath = Path.Combine(_tempDir, "movie.eng.sdh.idx");
        File.WriteAllBytes(subPath, Array.Empty<byte>());
        File.WriteAllBytes(idxPath, Array.Empty<byte>());

        ImageSubtitleConversionHelper.DeleteImageSubtitleSourceFiles(idxPath);

        Assert.False(File.Exists(subPath));
        Assert.False(File.Exists(idxPath));
    }

    [Fact]
    public void DeleteUnusedImageSubtitleSources_AutoWithSrtAndVobSub_DeletesSubAndIdx()
    {
        var srtPath = Path.Combine(_tempDir, "movie.eng.sdh.srt");
        var subPath = Path.Combine(_tempDir, "movie.2.eng.sdh.sub");
        var idxPath = Path.Combine(_tempDir, "movie.2.eng.sdh.idx");
        File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\nHi\n");
        File.WriteAllBytes(subPath, Array.Empty<byte>());
        File.WriteAllBytes(idxPath, Array.Empty<byte>());

        ImageSubtitleConversionHelper.DeleteUnusedImageSubtitleSources(
            [srtPath, subPath],
            SubtitleOcrMode.Auto,
            keepSource: false);

        Assert.True(File.Exists(srtPath));
        Assert.False(File.Exists(subPath));
        Assert.False(File.Exists(idxPath));
    }

    [Fact]
    public void DeleteUnusedImageSubtitleSources_WhenKeepSource_PreservesVobSubPair()
    {
        var srtPath = Path.Combine(_tempDir, "movie.eng.sdh.srt");
        var subPath = Path.Combine(_tempDir, "movie.2.eng.sdh.sub");
        var idxPath = Path.Combine(_tempDir, "movie.2.eng.sdh.idx");
        File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\nHi\n");
        File.WriteAllBytes(subPath, Array.Empty<byte>());
        File.WriteAllBytes(idxPath, Array.Empty<byte>());

        ImageSubtitleConversionHelper.DeleteUnusedImageSubtitleSources(
            [srtPath, subPath],
            SubtitleOcrMode.Auto,
            keepSource: true);

        Assert.True(File.Exists(srtPath));
        Assert.True(File.Exists(subPath));
        Assert.True(File.Exists(idxPath));
    }

    [Fact]
    public void DeleteUnusedImageSubtitleSources_WhenOcrSkip_PreservesVobSubPair()
    {
        var srtPath = Path.Combine(_tempDir, "movie.eng.sdh.srt");
        var subPath = Path.Combine(_tempDir, "movie.2.eng.sdh.sub");
        var idxPath = Path.Combine(_tempDir, "movie.2.eng.sdh.idx");
        File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\nHi\n");
        File.WriteAllBytes(subPath, Array.Empty<byte>());
        File.WriteAllBytes(idxPath, Array.Empty<byte>());

        ImageSubtitleConversionHelper.DeleteUnusedImageSubtitleSources(
            [srtPath, subPath],
            SubtitleOcrMode.Skip,
            keepSource: false);

        Assert.True(File.Exists(subPath));
        Assert.True(File.Exists(idxPath));
    }

    [Fact]
    public void ConvertToSrt_WhenSuccessful_DeletesSourceSupByDefault()
    {
        var supPath = Path.Combine(_tempDir, "movie.sup");
        var srtPath = Path.Combine(_tempDir, "movie.srt");
        File.WriteAllBytes(supPath, Array.Empty<byte>());

        _executableServiceMock
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, CancellationToken>((_, args, _) =>
            {
                File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\n\n");
            })
            .ReturnsAsync(new ExecutableResult(null, null, 0));

        ImageSubtitleConversionHelper.ConvertToSrt(
            _executableServiceMock.Object,
            @"C:\Program Files\Subtitle Edit\SubtitleEdit.exe",
            supPath,
            srtPath,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(srtPath));
        Assert.False(File.Exists(supPath));
    }

    [Fact]
    public void ConvertToSrt_WhenKeepSource_PreservesSupAndKeepsSrt()
    {
        var supPath = Path.Combine(_tempDir, "movie-keep.sup");
        var srtPath = Path.Combine(_tempDir, "movie-keep.srt");
        File.WriteAllBytes(supPath, Array.Empty<byte>());

        _executableServiceMock
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, CancellationToken>((_, args, _) =>
            {
                File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\n\n");
            })
            .ReturnsAsync(new ExecutableResult(null, null, 0));

        ImageSubtitleConversionHelper.ConvertToSrt(
            _executableServiceMock.Object,
            @"C:\Program Files\Subtitle Edit\SubtitleEdit.exe",
            supPath,
            srtPath,
            keepSource: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(srtPath));
        Assert.True(File.Exists(supPath));
    }

    [Fact]
    public void ConvertToSrt_WhenSuccessful_DeletesSubAndIdxCompanionByDefault()
    {
        var subPath = Path.Combine(_tempDir, "movie.eng.sdh.sub");
        var idxPath = Path.Combine(_tempDir, "movie.eng.sdh.idx");
        var srtPath = Path.Combine(_tempDir, "movie.eng.sdh.srt");
        File.WriteAllBytes(subPath, Array.Empty<byte>());
        File.WriteAllBytes(idxPath, Array.Empty<byte>());

        _executableServiceMock
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, CancellationToken>((_, args, _) =>
            {
                File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\n\n");
            })
            .ReturnsAsync(new ExecutableResult(null, null, 0));

        ImageSubtitleConversionHelper.ConvertToSrt(
            _executableServiceMock.Object,
            @"C:\Program Files\Subtitle Edit\SubtitleEdit.exe",
            subPath,
            srtPath,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(srtPath));
        Assert.False(File.Exists(subPath));
        Assert.False(File.Exists(idxPath));
    }

    [Fact]
    public void ConvertToSrt_WhenKeepSource_PreservesSubAndIdx()
    {
        var subPath = Path.Combine(_tempDir, "movie.keep.sub");
        var idxPath = Path.Combine(_tempDir, "movie.keep.idx");
        var srtPath = Path.Combine(_tempDir, "movie.keep.srt");
        File.WriteAllBytes(subPath, Array.Empty<byte>());
        File.WriteAllBytes(idxPath, Array.Empty<byte>());

        _executableServiceMock
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, CancellationToken>((_, args, _) =>
            {
                File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\n\n");
            })
            .ReturnsAsync(new ExecutableResult(null, null, 0));

        ImageSubtitleConversionHelper.ConvertToSrt(
            _executableServiceMock.Object,
            @"C:\Program Files\Subtitle Edit\SubtitleEdit.exe",
            subPath,
            srtPath,
            keepSource: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(srtPath));
        Assert.True(File.Exists(subPath));
        Assert.True(File.Exists(idxPath));
    }

    [Fact]
    public void ConvertToSrt_WhenSubtitleEditFails_DoesNotDeleteSourceFiles()
    {
        var supPath = Path.Combine(_tempDir, "movie.sup");
        File.WriteAllBytes(supPath, Array.Empty<byte>());

        _executableServiceMock
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(null, "failed", 1));

        Assert.Throws<InvalidOperationException>(() =>
            ImageSubtitleConversionHelper.ConvertToSrt(
                _executableServiceMock.Object,
                @"C:\Program Files\Subtitle Edit\SubtitleEdit.exe",
                supPath,
                Path.ChangeExtension(supPath, "srt")!,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.True(File.Exists(supPath));
    }

    [Fact]
    public void ConvertToSrt_WhenOutputMissing_DoesNotDeleteSourceFiles()
    {
        var supPath = Path.Combine(_tempDir, "movie.sup");
        File.WriteAllBytes(supPath, Array.Empty<byte>());

        _executableServiceMock
            .Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(null, null, 0));

        Assert.Throws<InvalidOperationException>(() =>
            ImageSubtitleConversionHelper.ConvertToSrt(
                _executableServiceMock.Object,
                @"C:\Program Files\Subtitle Edit\SubtitleEdit.exe",
                supPath,
                Path.ChangeExtension(supPath, "srt")!,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.True(File.Exists(supPath));
    }
}
