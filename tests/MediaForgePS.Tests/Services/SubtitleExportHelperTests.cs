using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SubtitleExportHelperTests
{
    private const string MkvextractPath = @"C:\tools\mkvextract.exe";

    private static MediaStream CreateStream(string codec, int index = 2, string? type = "subtitle", string? language = "eng") =>
        new(
            Type: type ?? string.Empty,
            Index: index,
            Codec: codec,
            Profile: string.Empty,
            CodecLongName: string.Empty,
            Tags: new Dictionary<string, string>(),
            Language: language);

    private static MediaFile CreateMediaFile(string path, params MediaStream[] streams) =>
        new(
            Path: path,
            Format: new MediaFormat(path, streams.Length, "matroska", "Matroska", 0, 1, 0, 0, new Dictionary<string, string>()),
            Chapters: [],
            Streams: streams,
            Raw: string.Empty);

    private static (Mock<IExecutableService> Mock, List<(string Exe, string[] Args)> Calls) CreateExecutableMock()
    {
        var mock = new Mock<IExecutableService>();
        var calls = new List<(string Exe, string[] Args)>();
        mock.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, CancellationToken>((exe, args, _) =>
                calls.Add((exe, args.ToArray())))
            .ReturnsAsync(new ExecutableResult(string.Empty, string.Empty, 0));

        return (mock, calls);
    }

    [Fact]
    public void ExtractSubtitle_MkvSource_VobSub_UsesMkvextract()
    {
        var (mock, calls) = CreateExecutableMock();
        var stream = CreateStream("dvd_subtitle");

        SubtitleExportHelper.ExtractSubtitle(
            mock.Object,
            stream,
            @"C:\media\movie.mkv",
            @"C:\out\movie.eng.sdh.sub",
            MkvextractPath,
            cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(calls);
        Assert.Equal(MkvextractPath, call.Exe);
        Assert.Contains("tracks", call.Args);
        Assert.Contains(@"C:\media\movie.mkv", call.Args);
        Assert.Contains(@"2:C:\out\movie.eng.sdh.sub", call.Args);
    }

    [Fact]
    public void ExtractSubtitle_MkvSource_VobSub_CaseInsensitiveExtension_UsesMkvextract()
    {
        var (mock, calls) = CreateExecutableMock();
        var stream = CreateStream("DVD_SUBTITLE");

        SubtitleExportHelper.ExtractSubtitle(
            mock.Object,
            stream,
            @"C:\media\movie.MKV",
            @"C:\out\movie.eng.sdh.sub",
            MkvextractPath,
            cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(calls);
        Assert.Equal(MkvextractPath, call.Exe);
    }

    [Fact]
    public void ExtractSubtitle_MkvSource_VobSub_MissingMkvextract_Throws()
    {
        var (mock, _) = CreateExecutableMock();
        var stream = CreateStream("dvd_subtitle");

        Assert.Throws<FileNotFoundException>(() =>
            SubtitleExportHelper.ExtractSubtitle(
                mock.Object,
                stream,
                @"C:\media\movie.mkv",
                @"C:\out\movie.eng.sdh.sub",
                mkvextractPath: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExtractSubtitle_MkvSource_NonVobSub_UsesFfmpeg()
    {
        var (mock, calls) = CreateExecutableMock();
        var stream = CreateStream("subrip");

        SubtitleExportHelper.ExtractSubtitle(
            mock.Object,
            stream,
            @"C:\media\movie.mkv",
            @"C:\out\movie.eng.sdh.srt",
            MkvextractPath,
            cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(calls);
        Assert.Equal("ffmpeg", call.Exe);
        Assert.Contains(@"C:\out\movie.eng.sdh.srt", call.Args);
        Assert.Contains("-c", call.Args);
        Assert.Contains("copy", call.Args);
    }

    [Fact]
    public void ExtractSubtitle_NonMkvSource_VobSub_FallsBackToFfmpeg_TargetingIdx()
    {
        var (mock, calls) = CreateExecutableMock();
        var stream = CreateStream("dvd_subtitle");

        SubtitleExportHelper.ExtractSubtitle(
            mock.Object,
            stream,
            @"C:\media\movie.vob",
            @"C:\out\movie.eng.sdh.sub",
            MkvextractPath,
            cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(calls);
        Assert.Equal("ffmpeg", call.Exe);
        Assert.Contains(@"C:\out\movie.eng.sdh.idx", call.Args);
        Assert.DoesNotContain(@"C:\out\movie.eng.sdh.sub", call.Args);
    }

    [Fact]
    public void ExtractSubtitle_NonMkvSource_VobSub_FallsBackToFfmpeg_EvenWithoutMkvextract()
    {
        var (mock, calls) = CreateExecutableMock();
        var stream = CreateStream("dvd_subtitle");

        SubtitleExportHelper.ExtractSubtitle(
            mock.Object,
            stream,
            @"C:\media\movie.mp4",
            @"C:\out\movie.eng.sdh.sub",
            mkvextractPath: null,
            cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(calls);
        Assert.Equal("ffmpeg", call.Exe);
    }

    [Fact]
    public void ExtractSubtitle_NonMkvSource_NonVobSub_UsesFfmpeg_TargetingOriginalPath()
    {
        var (mock, calls) = CreateExecutableMock();
        var stream = CreateStream("hdmv_pgs_subtitle");

        SubtitleExportHelper.ExtractSubtitle(
            mock.Object,
            stream,
            @"C:\media\movie.mp4",
            @"C:\out\movie.eng.sdh.sup",
            MkvextractPath,
            cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(calls);
        Assert.Equal("ffmpeg", call.Exe);
        Assert.Contains(@"C:\out\movie.eng.sdh.sup", call.Args);
    }

    [Fact]
    public void ExtractSubtitle_FfmpegFails_Throws()
    {
        var mock = new Mock<IExecutableService>();
        mock.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, "boom", 1));
        var stream = CreateStream("subrip");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SubtitleExportHelper.ExtractSubtitle(
                mock.Object,
                stream,
                @"C:\media\movie.mp4",
                @"C:\out\movie.eng.sdh.srt",
                MkvextractPath,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("FFmpeg failed", ex.Message);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public void ExtractSubtitle_MkvextractFails_Throws()
    {
        var mock = new Mock<IExecutableService>();
        mock.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutableResult(string.Empty, "broken", 2));
        var stream = CreateStream("dvd_subtitle");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SubtitleExportHelper.ExtractSubtitle(
                mock.Object,
                stream,
                @"C:\media\movie.mkv",
                @"C:\out\movie.eng.sdh.sub",
                MkvextractPath,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("mkvextract failed", ex.Message);
        Assert.Contains("broken", ex.Message);
    }

    // ---- GetEnglishSubtitleStreams ----

    [Fact]
    public void GetEnglishSubtitleStreams_ReturnsOnlyEnglishSubtitleStreams()
    {
        var media = CreateMediaFile(@"C:\media\movie.mkv",
            CreateStream("h264", index: 0, type: "video", language: "eng"),
            CreateStream("aac", index: 1, type: "audio", language: "eng"),
            CreateStream("subrip", index: 2, language: "eng"),
            CreateStream("subrip", index: 3, language: "spa"),
            CreateStream("hdmv_pgs_subtitle", index: 4, language: "en-US"),
            CreateStream("subrip", index: 5, language: null));

        var result = SubtitleExportHelper.GetEnglishSubtitleStreams(media);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Index == 2);
        Assert.Contains(result, s => s.Index == 4);
    }

    [Fact]
    public void GetEnglishSubtitleStreams_NullStreams_ReturnsEmpty()
    {
        var media = new MediaFile(@"C:\media\movie.mkv",
            new MediaFormat(@"C:\media\movie.mkv", 0, "matroska", "Matroska", 0, 1, 0, 0, new Dictionary<string, string>()),
            [], null!, string.Empty);

        var result = SubtitleExportHelper.GetEnglishSubtitleStreams(media);

        Assert.Empty(result);
    }

    // ---- ExtractEnglishSubtitles ----

    [Fact]
    public void ExtractEnglishSubtitles_NoEnglishStreams_InvokesCallbackAndReturnsEmpty()
    {
        var (mock, _) = CreateExecutableMock();
        var media = CreateMediaFile(@"C:\media\movie.mkv",
            CreateStream("subrip", index: 2, language: "spa"));
        var noEnglishCalled = false;

        var result = SubtitleExportHelper.ExtractEnglishSubtitles(
            mock.Object,
            media,
            MkvextractPath,
            buildOutputPath: _ => throw new InvalidOperationException("should not be called"),
            onNoEnglishSubtitles: () => noEnglishCalled = true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.True(noEnglishCalled);
        mock.Verify(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ExtractEnglishSubtitles_SingleSrt_OmitsStreamIndex_AndExtracts()
    {
        var (mock, calls) = CreateExecutableMock();
        var media = CreateMediaFile(@"C:\media\movie.mkv",
            CreateStream("subrip", index: 2, language: "eng"));

        var result = SubtitleExportHelper.ExtractEnglishSubtitles(
            mock.Object,
            media,
            MkvextractPath,
            buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                media.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
            cancellationToken: TestContext.Current.CancellationToken);

        var path = Assert.Single(result);
        Assert.Equal(@"C:\media\movie.eng.sdh.srt", path);
        var call = Assert.Single(calls);
        Assert.Equal("ffmpeg", call.Exe);
        Assert.Contains(@"C:\media\movie.eng.sdh.srt", call.Args);
    }

    [Fact]
    public void ExtractEnglishSubtitles_MixedExtensions_OmitsIndexForLoneTextSrt_IndexesImageTrack()
    {
        var (mock, calls) = CreateExecutableMock();
        var media = CreateMediaFile(@"C:\media\movie.mkv",
            CreateStream("subrip", index: 2, language: "eng"),
            CreateStream("hdmv_pgs_subtitle", index: 3, language: "eng"));

        var result = SubtitleExportHelper.ExtractEnglishSubtitles(
            mock.Object,
            media,
            MkvextractPath,
            buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                media.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(@"C:\media\movie.eng.sdh.srt", result);
        Assert.Contains(@"C:\media\movie.3.eng.sdh.sup", result);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public void GetOutputPath_MixedTextAndImageSubtitles_OcrTargetDoesNotCollideWithTextSrt()
    {
        var textSrt = SubtitleExportHelper.GetOutputPath(@"C:\out\foo.mp4", streamIndex: 2, sameExtensionCount: 1, extension: "srt", englishSubtitleCount: 2);
        var imageSup = SubtitleExportHelper.GetOutputPath(@"C:\out\foo.mp4", streamIndex: 3, sameExtensionCount: 1, extension: "sup", englishSubtitleCount: 2);
        var ocrSrt = Path.ChangeExtension(imageSup, "srt");

        Assert.Equal(@"C:\out\foo.eng.sdh.srt", textSrt);
        Assert.Equal(@"C:\out\foo.3.eng.sdh.sup", imageSup);
        Assert.Equal(@"C:\out\foo.3.eng.sdh.srt", ocrSrt);
        Assert.NotEqual(textSrt, ocrSrt);
    }

    [Fact]
    public void GetOutputPath_SingleSrtAmongMultipleEnglishSubtitles_OmitsStreamIndex()
    {
        var path = SubtitleExportHelper.GetOutputPath(@"C:\out\foo.mp4", streamIndex: 6, sameExtensionCount: 1, extension: "srt", englishSubtitleCount: 3);

        Assert.Equal(@"C:\out\foo.eng.sdh.srt", path);
    }

    [Fact]
    public void ExtractEnglishSubtitles_MultipleSameExtension_IncludesStreamIndex()
    {
        var (mock, _) = CreateExecutableMock();
        var media = CreateMediaFile(@"C:\media\movie.mkv",
            CreateStream("subrip", index: 2, language: "eng"),
            CreateStream("subrip", index: 3, language: "eng"));

        var result = SubtitleExportHelper.ExtractEnglishSubtitles(
            mock.Object,
            media,
            MkvextractPath,
            buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                media.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(@"C:\media\movie.2.eng.sdh.srt", result);
        Assert.Contains(@"C:\media\movie.3.eng.sdh.srt", result);
    }

    [Fact]
    public void ExtractEnglishSubtitles_UnknownCodec_InvokesCallbackAndUsesBin()
    {
        var (mock, _) = CreateExecutableMock();
        var media = CreateMediaFile(@"C:\media\movie.mkv",
            CreateStream("mystery_codec", index: 2, language: "eng"));
        var reported = new List<MediaStream>();

        var result = SubtitleExportHelper.ExtractEnglishSubtitles(
            mock.Object,
            media,
            MkvextractPath,
            buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                media.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
            onUnknownCodec: stream => reported.Add(stream),
            cancellationToken: TestContext.Current.CancellationToken);

        var path = Assert.Single(result);
        Assert.EndsWith(".bin", path);
        var reportedStream = Assert.Single(reported);
        Assert.Equal("mystery_codec", reportedStream.Codec);
    }

    [Fact]
    public void ExtractEnglishSubtitles_FinalizeReturnsNull_SkipsStream()
    {
        var (mock, calls) = CreateExecutableMock();
        var media = CreateMediaFile(@"C:\media\movie.mkv",
            CreateStream("subrip", index: 2, language: "eng"),
            CreateStream("subrip", index: 3, language: "eng"));

        var result = SubtitleExportHelper.ExtractEnglishSubtitles(
            mock.Object,
            media,
            MkvextractPath,
            buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                media.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
            finalizeOutputPath: candidate => candidate.Contains(".2.") ? null : candidate,
            cancellationToken: TestContext.Current.CancellationToken);

        var path = Assert.Single(result);
        Assert.Equal(@"C:\media\movie.3.eng.sdh.srt", path);
        Assert.Single(calls);
    }

    [Fact]
    public void ExtractEnglishSubtitles_ExtractionThrows_InvokesCallbackAndContinues()
    {
        var mock = new Mock<IExecutableService>();
        mock.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IEnumerable<string> args, CancellationToken _) =>
                args.Any(a => a.Contains(".2.")) ? new ExecutableResult(string.Empty, "boom", 1) : new ExecutableResult(string.Empty, string.Empty, 0));

        var media = CreateMediaFile(@"C:\media\movie.mkv",
            CreateStream("subrip", index: 2, language: "eng"),
            CreateStream("subrip", index: 3, language: "eng"));
        var failures = new List<(MediaStream, Exception)>();

        var result = SubtitleExportHelper.ExtractEnglishSubtitles(
            mock.Object,
            media,
            MkvextractPath,
            buildOutputPath: plan => SubtitleExportHelper.GetOutputPath(
                media.Path, plan.Stream.Index, plan.SameExtensionCount, plan.Extension, plan.EnglishSubtitleCount),
            onExtractFailed: (s, ex) => failures.Add((s, ex)),
            cancellationToken: TestContext.Current.CancellationToken);

        var path = Assert.Single(result);
        Assert.Contains(".3.", path);
        var (failedStream, failedEx) = Assert.Single(failures);
        Assert.Equal(2, failedStream.Index);
        Assert.IsType<InvalidOperationException>(failedEx);
    }
}
