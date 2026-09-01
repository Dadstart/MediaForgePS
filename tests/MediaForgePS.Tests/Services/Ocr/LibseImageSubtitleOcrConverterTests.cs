using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Sdk;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ocr;

[SupportedOSPlatform("windows")]
public class LibseImageSubtitleOcrConverterTests : IDisposable
{
    private readonly string _tempDir;

    public LibseImageSubtitleOcrConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_LibseOcr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ConvertToSrt_WhenLanguageDataMissing_ThrowsFileNotFound()
    {
        var converter = new LibseImageSubtitleOcrConverter(NullLogger<LibseImageSubtitleOcrConverter>.Instance, "zz_missing_lang");
        Assert.False(converter.IsAvailable);

        var ex = Assert.Throws<FileNotFoundException>(() =>
            converter.ConvertToSrt(Path.Combine(_tempDir, "missing.sup"), Path.Combine(_tempDir, "out.srt"), TestContext.Current.CancellationToken));

        Assert.Contains("Tesseract language data not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConvertToSrt_WhenInputMissing_AndTessDataAvailable_ThrowsFileNotFound()
    {
        var converter = CreateConverterOrSkip();
        var missing = Path.Combine(_tempDir, "does-not-exist.sup");
        var output = Path.Combine(_tempDir, "out.srt");

        var ex = Assert.Throws<FileNotFoundException>(() => converter.ConvertToSrt(missing, output, TestContext.Current.CancellationToken));
        Assert.Contains(missing, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConvertToSrt_WhenUnsupportedExtension_ThrowsNotSupported()
    {
        var converter = CreateConverterOrSkip();
        var input = Path.Combine(_tempDir, "movie.srt");
        File.WriteAllText(input, "1\n00:00:00,000 --> 00:00:01,000\nHi\n");

        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertToSrt(input, Path.Combine(_tempDir, "out.srt"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ConvertToSrt_WhenVobSubIdxMissing_ThrowsFileNotFound()
    {
        var converter = CreateConverterOrSkip();
        var subPath = Path.Combine(_tempDir, "movie.sub");
        File.WriteAllBytes(subPath, [0x00]);

        var ex = Assert.Throws<FileNotFoundException>(() =>
            converter.ConvertToSrt(subPath, Path.Combine(_tempDir, "out.srt"), TestContext.Current.CancellationToken));

        Assert.Contains(".idx", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsPalFromIdxFile_WhenNtscSize_ReturnsFalse()
    {
        var idxPath = WriteIdxWithSize("720x480");
        Assert.False(LibseImageSubtitleOcrConverter.IsPalFromIdxFile(idxPath));
    }

    [Fact]
    public void IsPalFromIdxFile_WhenPalSize_ReturnsTrue()
    {
        var idxPath = WriteIdxWithSize("720x576");
        Assert.True(LibseImageSubtitleOcrConverter.IsPalFromIdxFile(idxPath));
    }

    [Fact]
    public void IsPalFromIdxFile_WhenSizeMissing_ReturnsFalse()
    {
        var idxPath = Path.Combine(_tempDir, "nosize.idx");
        File.WriteAllText(idxPath, "# VobSub index file, v7\r\nid: en, index: 0\r\n");
        Assert.False(LibseImageSubtitleOcrConverter.IsPalFromIdxFile(idxPath));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void LoadMergedVobSubPacks_AssignsIdxPaletteToEachPack()
    {
        if (!OperatingSystem.IsWindows())
            throw SkipException.ForSkip("VobSub fixture generation requires Windows System.Drawing.");

        var subPath = ImageSubtitleTestAssetFactory.CreateVobSubWithText(_tempDir, "palette", "HELLO");
        var packs = LibseImageSubtitleOcrConverter.LoadMergedVobSubPacks(subPath);

        Assert.NotEmpty(packs);
        Assert.All(packs, pack =>
        {
            Assert.NotNull(pack.Palette);
            Assert.True(pack.Palette.Count >= 4, "IDX palette should include the four DVD CLUT colors.");
        });
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void LoadMergedVobSubPacks_UsesIdxPatternColorForGlyphs()
    {
        if (!OperatingSystem.IsWindows())
            throw SkipException.ForSkip("VobSub fixture generation requires Windows System.Drawing.");

        var subPath = ImageSubtitleTestAssetFactory.CreateVobSubWithText(_tempDir, "glyphs", "HELLO");
        var packs = LibseImageSubtitleOcrConverter.LoadMergedVobSubPacks(subPath);
        var pack = Assert.Single(packs);

        Assert.NotNull(pack.Palette);
        Assert.Equal(Color.FromArgb(0, 0, 0), pack.Palette[0]);
        Assert.Equal(Color.FromArgb(255, 255, 255), pack.Palette[1]);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void ConvertToSrt_WithGeneratedVobSub_WritesNonEmptySrt()
    {
        if (!OperatingSystem.IsWindows())
            throw SkipException.ForSkip("VobSub fixture generation requires Windows System.Drawing.");

        var converter = CreateConverterOrSkip();
        var subPath = ImageSubtitleTestAssetFactory.CreateVobSubWithText(_tempDir, "hello", "HELLO");
        var srtPath = Path.Combine(_tempDir, "hello.srt");

        converter.ConvertToSrt(subPath, srtPath, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(srtPath));
        var content = File.ReadAllText(srtPath);
        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains("HELLO", content, StringComparison.OrdinalIgnoreCase);
    }

    private string WriteIdxWithSize(string size)
    {
        var idxPath = Path.Combine(_tempDir, size.Replace('x', '_') + ".idx");
        File.WriteAllText(idxPath, $"# VobSub index file, v7\r\nsize: {size}\r\nid: en, index: 0\r\n");
        return idxPath;
    }

    private static LibseImageSubtitleOcrConverter CreateConverterOrSkip()
    {
        var converter = new LibseImageSubtitleOcrConverter(NullLogger<LibseImageSubtitleOcrConverter>.Instance);
        if (!converter.IsAvailable)
            throw SkipException.ForSkip($"Tesseract language data not found. {converter.ExpectedTessDataDescription}");

        return converter;
    }
}

