using System;
using System.IO;
using System.Runtime.Versioning;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.VobSub;
using SkiaSharp;

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
            pattern: SKColors.White,
            emphasis1: SKColors.Black,
            useInnerAntiAliasing: true,
            language: DvdSubtitleLanguage.English);

        using var bitmap = CreateTextBitmap(text);
        var paragraph = new Paragraph(text, 0, 1000);
        writer.WriteParagraph(paragraph, bitmap, BluRayContentAlignment.BottomCenter, null);
        writer.WriteIdxFile();

        AssertFilesExist(subPath);
        return subPath;
    }

    private static SKBitmap CreateTextBitmap(string text)
    {
        var bitmap = new SKBitmap(320, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var font = new SKFont(typeface, 36);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(text, 8, 56, SKTextAlign.Left, font, paint);
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
