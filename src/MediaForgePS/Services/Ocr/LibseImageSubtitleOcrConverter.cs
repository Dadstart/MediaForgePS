using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using Dadstart.Labs.MediaForge.Services;
using Microsoft.Extensions.Logging;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Core.VobSub;
using SkiaSharp;
using Tesseract;

namespace Dadstart.Labs.MediaForge.Services.Ocr;

/// <summary>
/// Converts SUP and VobSub (SUB/IDX) image subtitles to SRT using libse for parsing/writing and Tesseract for OCR.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LibseImageSubtitleOcrConverter : IImageSubtitleOcrConverter
{
    private readonly ILogger<LibseImageSubtitleOcrConverter> _logger;
    private readonly string _language;
    private readonly string? _tessDataPath;

    public LibseImageSubtitleOcrConverter(ILogger<LibseImageSubtitleOcrConverter> logger)
        : this(logger, TesseractDataPathResolver.DefaultLanguage)
    {
    }

    public LibseImageSubtitleOcrConverter(ILogger<LibseImageSubtitleOcrConverter> logger, string language)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _language = string.IsNullOrWhiteSpace(language) ? TesseractDataPathResolver.DefaultLanguage : language;
        _tessDataPath = TesseractDataPathResolver.ResolveTessDataPath(_language);
    }

    /// <inheritdoc />
    public bool IsSupportedOnCurrentPlatform => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public bool IsAvailable => IsSupportedOnCurrentPlatform && !string.IsNullOrEmpty(_tessDataPath);

    /// <inheritdoc />
    public string ExpectedTessDataDescription =>
        TesseractDataPathResolver.GetExpectedLocationsDescription(_language);

    /// <inheritdoc />
    public void ConvertToSrt(string inputPath, string outputSrtPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSrtPath);

        if (!IsAvailable || string.IsNullOrEmpty(_tessDataPath))
            throw new FileNotFoundException($"Tesseract language data not found. {ExpectedTessDataDescription}");

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Image subtitle file not found: {inputPath}", inputPath);

        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(inputPath);
        var subtitle = extension.Equals(".sup", StringComparison.OrdinalIgnoreCase)
            ? OcrBluRaySup(inputPath, cancellationToken)
            : extension.Equals(".sub", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".idx", StringComparison.OrdinalIgnoreCase)
                ? OcrVobSub(inputPath, cancellationToken)
                : throw new NotSupportedException($"Unsupported image subtitle extension '{extension}' for OCR conversion.");

        if (subtitle.Paragraphs.Count == 0)
            throw new InvalidOperationException($"OCR produced no subtitle lines for: {inputPath}");

        var srtText = new SubRip().ToText(subtitle, Path.GetFileNameWithoutExtension(outputSrtPath) ?? "untitled");
        AtomicFileHelper.WriteTextAtomically(outputSrtPath, srtText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), overwrite: true);
        _logger.LogDebug("Wrote OCR SRT with {Count} lines: {Path}", subtitle.Paragraphs.Count, outputSrtPath);
    }

    private Subtitle OcrBluRaySup(string inputPath, CancellationToken cancellationToken)
    {
        var log = new StringBuilder();
        var pcsList = BluRaySupParser.ParseBluRaySup(inputPath, log);
        if (pcsList.Count == 0)
            throw new InvalidOperationException($"No Blu-ray SUP pictures found in: {inputPath}");

        var frames = new List<OcrFrame>(pcsList.Count);
        foreach (var pcs in pcsList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(new OcrFrame(pcs.StartTimeCode, pcs.EndTimeCode, () => pcs.GetBitmap()));
        }

        return RunOcr(frames, cancellationToken);
    }

    private Subtitle OcrVobSub(string inputPath, CancellationToken cancellationToken)
    {
        var packs = LoadMergedVobSubPacks(inputPath);
        if (packs.Count == 0)
            throw new InvalidOperationException($"No VobSub pictures found in: {inputPath}");

        if (packs[0].Palette is null || packs[0].Palette.Count == 0)
            _logger.LogWarning("VobSub idx has no palette; OCR glyphs may be unreadable: {Path}", inputPath);

        var frames = new List<OcrFrame>(packs.Count);
        foreach (var pack in packs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(new OcrFrame(pack.StartTimeCode, pack.EndTimeCode, () => GetOcrBitmap(pack)));
        }

        return RunOcr(frames, cancellationToken);
    }

    /// <summary>
    /// Parses a VobSub pair and applies the IDX CLUT to each merged pack so contrast/alpha from the
    /// subpicture can be applied. Glyph colors used for OCR are isolated separately.
    /// </summary>
    internal static List<VobSubMergedPack> LoadMergedVobSubPacks(string inputPath)
    {
        ResolveVobSubPaths(inputPath, out var subPath, out var idxPath);
        if (!File.Exists(subPath))
            throw new FileNotFoundException($"VobSub .sub file not found: {subPath}", subPath);
        if (!File.Exists(idxPath))
            throw new FileNotFoundException($"VobSub .idx file not found: {idxPath}", idxPath);

        var parser = new VobSubParser(isPal: IsPalFromIdxFile(idxPath));
        parser.OpenSubIdx(subPath, idxPath);
        var packs = parser.MergeVobSubPacks();
        foreach (var pack in packs)
            pack.Palette = parser.IdxPalette;

        return packs;
    }

    /// <summary>
    /// DVD PAL is 720x576; NTSC is 720x480. libse uses this only for PTS conversion.
    /// </summary>
    internal static bool IsPalFromIdxFile(string idxPath)
    {
        foreach (var line in File.ReadLines(idxPath))
        {
            var trimmed = line.AsSpan().Trim();
            if (!trimmed.StartsWith("size:", StringComparison.OrdinalIgnoreCase))
                continue;

            return trimmed.Contains("x576", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Renders a VobSub picture using the IDX palette so glyph fill is not the default black.
    /// </summary>
    internal static SKBitmap GetOcrBitmap(VobSubMergedPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.GetBitmap();
    }

    private Subtitle RunOcr(IReadOnlyList<OcrFrame> frames, CancellationToken cancellationToken)
    {
        var subtitle = new Subtitle();
        using var engine = new TesseractEngine(_tessDataPath, _language, EngineMode.Default);

        for (var i = 0; i < frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = frames[i];
            using var bitmap = frame.GetBitmap();
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
                continue;

            var text = OcrBitmap(engine, bitmap);
            text = string.Join(Environment.NewLine, text.SplitToLines()).Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            subtitle.Paragraphs.Add(new Paragraph(text.Trim(), frame.Start.TotalMilliseconds, frame.End.TotalMilliseconds)
            {
                Number = subtitle.Paragraphs.Count + 1
            });
        }

        return subtitle;
    }

    private static string OcrBitmap(TesseractEngine engine, SKBitmap bitmap)
    {
        using var opaque = FlattenOntoBlack(bitmap);
        using var inverted = InvertRgb(opaque);
        var text = ProcessPix(engine, inverted);
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        return ProcessPix(engine, opaque);
    }

    private static string ProcessPix(TesseractEngine engine, SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
            return string.Empty;

        using var pix = Pix.LoadFromMemory(data.ToArray());
        using var page = engine.Process(pix, PageSegMode.SingleBlock);
        return page.GetText() ?? string.Empty;
    }

    private static SKBitmap FlattenOntoBlack(SKBitmap source)
    {
        const int Margin = 10;
        var result = new SKBitmap(source.Width + (Margin * 2), source.Height + (Margin * 2));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);
        canvas.DrawBitmap(source, Margin, Margin);
        return result;
    }

    private static SKBitmap InvertRgb(SKBitmap source)
    {
        var result = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(result);
        using var filter = SKColorFilter.CreateColorMatrix(
        [
            -1f, 0f, 0f, 0f, 1f,
            0f, -1f, 0f, 0f, 1f,
            0f, 0f, -1f, 0f, 1f,
            0f, 0f, 0f, 1f, 0f
        ]);
        using var paint = new SKPaint { ColorFilter = filter };
        canvas.DrawBitmap(source, 0, 0, paint);
        return result;
    }

    private static void ResolveVobSubPaths(string inputPath, out string subPath, out string idxPath)
    {
        var extension = Path.GetExtension(inputPath);
        if (extension.Equals(".sub", StringComparison.OrdinalIgnoreCase))
        {
            subPath = inputPath;
            idxPath = Path.ChangeExtension(inputPath, ".idx")
                ?? throw new InvalidOperationException($"Could not resolve .idx companion for: {inputPath}");
            return;
        }

        if (extension.Equals(".idx", StringComparison.OrdinalIgnoreCase))
        {
            idxPath = inputPath;
            subPath = Path.ChangeExtension(inputPath, ".sub")
                ?? throw new InvalidOperationException($"Could not resolve .sub companion for: {inputPath}");
            return;
        }

        throw new NotSupportedException($"Unsupported VobSub path: {inputPath}");
    }

    private readonly record struct OcrFrame(TimeCode Start, TimeCode End, Func<SKBitmap> GetBitmap);
}
