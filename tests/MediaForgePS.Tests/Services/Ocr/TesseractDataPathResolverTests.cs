using System;
using System.IO;
using Dadstart.Labs.MediaForge.Services.Ocr;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ocr;

public class TesseractDataPathResolverTests
{
    [Fact]
    public void GetExpectedLocationsDescription_IncludesLanguageAndCandidates()
    {
        var description = TesseractDataPathResolver.GetExpectedLocationsDescription("eng");

        Assert.Contains("eng.traineddata", description, StringComparison.Ordinal);
        Assert.Contains("TESSDATA_PREFIX", description, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveTessDataPath_WhenUnavailable_ReturnsNullOrExistingPath()
    {
        var path = TesseractDataPathResolver.ResolveTessDataPath("eng");
        if (path is null)
            return;

        Assert.True(Directory.Exists(path));
        Assert.True(File.Exists(Path.Combine(path, "eng.traineddata")));
    }

    [Fact]
    public void ResolveTessDataPath_WhenLanguageMissing_ReturnsNull()
    {
        Assert.Null(TesseractDataPathResolver.ResolveTessDataPath("zz_missing_lang"));
    }

    [Fact]
    public void DefaultLanguage_IsEnglish()
    {
        Assert.Equal("eng", TesseractDataPathResolver.DefaultLanguage);
    }
}
