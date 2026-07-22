using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.VobSub;

namespace Dadstart.Labs.MediaForge.Tests.TestInfrastructure;

/// <summary>
/// Builds tiny image-subtitle fixtures for OCR tests using libse writers.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ImageSubtitleTestAssetFactory
{
    /// <summary>
    /// Writes a one-line VobSub (.sub/.idx) pair whose bitmap contains <paramref name="text"/>.
    /// Returns the .sub path.
    /// </summary>
    public static string CreateVobSubWithText(string directory, string baseName, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Directory.CreateDirectory(directory);
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

        using var bitmap = CreateTextBitmap(text);
        var paragraph = new Paragraph(text, 0, 1000);
        writer.WriteParagraph(paragraph, bitmap, ContentAlignment.BottomCenter, null);
        writer.WriteIdxFile();

        AssertFilesExist(subPath);
        return subPath;
    }

    private static Bitmap CreateTextBitmap(string text)
    {
        var bitmap = new Bitmap(320, 80);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        using var font = new Font(FontFamily.GenericSansSerif, 36, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        graphics.DrawString(text, font, brush, x: 8, y: 16);
        return bitmap;
    }

    private static void AssertFilesExist(string subPath)
    {
        if (!File.Exists(subPath))
            throw new InvalidOperationException($"Expected VobSub file was not created: {subPath}");

        var idxPath = Path.ChangeExtension(subPath, ".idx");
        if (string.IsNullOrEmpty(idxPath) || !File.Exists(idxPath))
            throw new InvalidOperationException($"Expected VobSub idx companion was not created for: {subPath}");
    }
}
