using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.VobSub;
using Xunit;
using Xunit.Sdk;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class InvokeSubtitleOcrRepairCommandComponentTests : ComponentTestBase
{
    [Fact]
    public void InvokeSubtitleOcrRepair_WithExistingSrtOnly_SkipRepairDoesNotModifyPreExistingSrt()
    {
        SkipIfOcrBrokenSrtAssetMissing();

        var srtPath = CopyOcrBrokenSrtAs("ocr-repair-existing.srt");
        var original = File.ReadAllText(srtPath);

        using var ps = CreatePowerShellFor<InvokeSubtitleOcrRepairCommand>("Invoke-SubtitleOcrRepair");
        ps.AddCommand("Invoke-SubtitleOcrRepair")
            .AddParameter("InputPath", srtPath)
            .AddParameter("SkipRepair")
            .AddParameter("Confirm", false);

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results));
        Assert.Equal(0, result.ConvertedCount);
        Assert.Equal(original, File.ReadAllText(srtPath));
    }

    [Fact(Timeout = 120_000)]
    [SupportedOSPlatform("windows")]
    public void InvokeSubtitleOcrRepair_WithGeneratedVobSub_ConvertsAndRepairsOcrOutput()
    {
        if (!OperatingSystem.IsWindows())
            throw SkipException.ForSkip("Image subtitle OCR component tests require Windows.");

        SkipIfTesseractDataMissing();

        var directory = CreateTempDirectory();
        var subPath = CreateVobSubWithText(directory, "ocr-repair-hello", "HELLO");
        var expectedSrt = Path.ChangeExtension(subPath, "srt")!;

        using var ps = CreatePowerShellFor<InvokeSubtitleOcrRepairCommand>("Invoke-SubtitleOcrRepair");
        ps.AddCommand("Invoke-SubtitleOcrRepair")
            .AddParameter("InputPath", subPath)
            .AddParameter("KeepSource", true)
            .AddParameter("Confirm", false);

        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results));
        Assert.Equal(1, result.ConvertedCount);
        Assert.Equal(expectedSrt, Assert.Single(result.ConvertedPaths));
        Assert.True(File.Exists(expectedSrt));
        Assert.Contains("HELLO", File.ReadAllText(expectedSrt), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(subPath));
    }

    private void SkipIfOcrBrokenSrtAssetMissing()
    {
        if (File.Exists(OcrBrokenSrtPath))
            return;

        FailOrSkip("Component test SRT asset is missing. Add ocr-broken.srt under TestAssets.");
    }

    private static void SkipIfTesseractDataMissing()
    {
        if (TesseractDataPathResolver.ResolveTessDataPath() is not null)
            return;

        FailOrSkip(
            "Tesseract language data (eng.traineddata) not found. " +
            TesseractDataPathResolver.GetExpectedLocationsDescription());
    }

    [SupportedOSPlatform("windows")]
    private static string CreateVobSubWithText(string directory, string baseName, string text)
    {
        var subPath = Path.Combine(directory, baseName + ".sub");
        using var writer = new VobSubWriter(
            subPath,
            screenWidth: 720,
            screenHeight: 480,
            bottomMargin: 20,
            leftRightMargin: 10,
            languageStreamId: 0x20,
            pattern: Color.White,
            emphasis1: Color.Black,
            useInnerAntiAliasing: true,
            language: DvdSubtitleLanguage.English);

        using var bitmap = new Bitmap(320, 80);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            using var font = new Font(FontFamily.GenericSansSerif, 36, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.White);
            graphics.DrawString(text, font, brush, x: 8, y: 16);
        }

        writer.WriteParagraph(new Paragraph(text, 0, 1000), bitmap, ContentAlignment.BottomCenter, null);
        writer.WriteIdxFile();
        return subPath;
    }
}
