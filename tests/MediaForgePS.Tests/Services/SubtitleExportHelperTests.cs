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

    private static MediaStream CreateStream(string codec, int index = 2) =>
        new(
            Type: "subtitle",
            Index: index,
            Codec: codec,
            Profile: string.Empty,
            CodecLongName: string.Empty,
            Tags: new Dictionary<string, string>(),
            Language: "eng");

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
            MkvextractPath);

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
            MkvextractPath);

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
                mkvextractPath: null));
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
            MkvextractPath);

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
            MkvextractPath);

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
            mkvextractPath: null);

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
            MkvextractPath);

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
                MkvextractPath));
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
                MkvextractPath));
        Assert.Contains("mkvextract failed", ex.Message);
        Assert.Contains("broken", ex.Message);
    }
}
