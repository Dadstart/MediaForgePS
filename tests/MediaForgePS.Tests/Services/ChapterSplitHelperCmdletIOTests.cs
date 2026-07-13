using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class ChapterSplitHelperCmdletIOTests
{
    [Fact]
    public void TryGetChapters_WithMissingChapters_WritesErrorViaICmdletIO()
    {
        var io = new FakeCmdletIO();
        var media = new MediaFile(
            "video.mkv",
            new MediaFormat("video.mkv", 1, "matroska", "Matroska", 0, 100, 1000, 1000, new Dictionary<string, string>()),
            Array.Empty<MediaChapter>(),
            Array.Empty<MediaStream>(),
            "{}");

        var ok = ChapterSplitHelper.TryGetChapters(io, "video.mkv", media, out var chapters);

        Assert.False(ok);
        Assert.Empty(chapters);
        var error = Assert.Single(io.Errors);
        Assert.Contains("NoChapters", error.FullyQualifiedErrorId, StringComparison.Ordinal);
        Assert.Equal(ErrorCategory.InvalidOperation, error.CategoryInfo.Category);
    }
}
