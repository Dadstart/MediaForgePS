using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dadstart.Labs.MediaForge.Services.Ocr;

/// <summary>
/// Resolves the Tesseract <c>tessdata</c> directory used for image subtitle OCR.
/// </summary>
public static class TesseractDataPathResolver
{
    public const string DefaultLanguage = "eng";

    /// <summary>
    /// Returns the first existing tessdata directory that contains <paramref name="language"/>.traineddata,
    /// or null when none is found.
    /// </summary>
    public static string? ResolveTessDataPath(string language = DefaultLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        var trainedDataFile = language + ".traineddata";
        foreach (var candidate in EnumerateCandidateDirectories())
        {
            if (File.Exists(Path.Combine(candidate, trainedDataFile)))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Describes expected tessdata locations for error messages.
    /// </summary>
    public static string GetExpectedLocationsDescription(string language = DefaultLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        var locations = EnumerateCandidateDirectories().Distinct(StringComparer.OrdinalIgnoreCase);
        return $"Set TESSDATA_PREFIX or install {language}.traineddata under one of: {string.Join("; ", locations)}";
    }

    private static IEnumerable<string> EnumerateCandidateDirectories()
    {
        var tessDataPrefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        if (!string.IsNullOrWhiteSpace(tessDataPrefix))
        {
            yield return tessDataPrefix.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            yield return Path.Combine(tessDataPrefix, "tessdata");
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            yield return Path.Combine(programFiles, "Tesseract-OCR", "tessdata");

            var subtitleEditRoot = Path.Combine(programFiles, "Subtitle Edit");
            if (Directory.Exists(subtitleEditRoot))
            {
                foreach (var tesseractDir in Directory.EnumerateDirectories(subtitleEditRoot, "Tesseract*"))
                {
                    yield return Path.Combine(tesseractDir, "tessdata");
                    yield return tesseractDir;
                }
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            yield return Path.Combine(localAppData, "Tesseract-OCR", "tessdata");
        }
        else
        {
            yield return "/usr/share/tesseract-ocr/5/tessdata";
            yield return "/usr/share/tesseract-ocr/4.00/tessdata";
            yield return "/usr/share/tessdata";
            yield return "/usr/local/share/tessdata";
        }
    }
}
