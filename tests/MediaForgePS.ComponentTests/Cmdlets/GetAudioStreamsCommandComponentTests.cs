using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class GetAudioStreamsCommandComponentTests : ComponentTestBase
{
    [Fact(Timeout = 60_000)]
    public void GetAudioStreams_WithEnglishAudioSample_ReturnsAudioTrackMappings()
    {
        SkipIfMediaToolsMissing();
        SkipIfTestAssetsMissing();

        var inputPath = CreateSampleVideoWithEnglishAudio("audio-eng.mkv");

        using var ps = CreatePowerShellFor<GetAudioTrackMappingsCommand>("Get-AudioStreams");
        ps.AddCommand("Get-AudioStreams").AddParameter("InputPath", inputPath);

        var results = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var mappings = Assert.IsType<AudioTrackMapping[]>(Assert.Single(results).BaseObject);
        Assert.NotEmpty(mappings);
        Assert.All(mappings, mapping => Assert.True(mapping.SourceIndex >= 0));
    }
}
