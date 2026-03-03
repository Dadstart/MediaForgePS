using System.Linq;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.Ffmpeg;

public class FfmpegArgumentBuilderTests
{
    [Fact]
    public void AddTitleMetadata_DoesNotPreQuoteTitleValue()
    {
        var args = new FfmpegArgumentBuilder()
            .AddTitleMetadata('a', 0, "English \"5.1\"")
            .ToArguments()
            .ToArray();

        Assert.Equal(["-metadata:s:a:0", "title=English \"5.1\""], args);
    }
}
