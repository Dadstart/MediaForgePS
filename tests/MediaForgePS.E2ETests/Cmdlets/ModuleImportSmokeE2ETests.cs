using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.E2ETests.Cmdlets;

public class ModuleImportSmokeE2ETests : E2ETestBase
{
    [Fact(Timeout = 180_000)]
    public void PackedModule_ImportGetMediaFileAndFactoryCmdlets_Succeed()
    {
        using var ps = ImportPackedModule();

        ps.AddCommand("Get-MediaFile").AddParameter("Path", SampleVideoPath);
        var mediaResults = ps.Invoke().ToList();
        var mediaErrors = ps.Streams.Error.ReadAll();
        Assert.Empty(mediaErrors);
        var mediaFile = Assert.IsType<MediaFile>(Assert.Single(mediaResults).BaseObject);
        Assert.True(File.Exists(mediaFile.Path));
        Assert.NotNull(mediaFile.Format);
        ps.Commands.Clear();

        ps.AddCommand("New-AudioTrackMapping")
            .AddParameter("SourceStream", 0)
            .AddParameter("SourceIndex", 0)
            .AddParameter("DestinationIndex", 0)
            .AddParameter("Copy", true);
        var mappingResults = ps.Invoke().ToList();
        var mappingErrors = ps.Streams.Error.ReadAll();
        Assert.Empty(mappingErrors);
        Assert.IsType<CopyAudioTrackMapping>(Assert.Single(mappingResults).BaseObject);
        ps.Commands.Clear();

        ps.AddCommand("New-VideoEncodingSettings")
            .AddParameter("Codec", "libx264")
            .AddParameter("CRF", 28)
            .AddParameter("Preset", "veryfast");
        var settingsResults = ps.Invoke().ToList();
        var settingsErrors = ps.Streams.Error.ReadAll();
        Assert.Empty(settingsErrors);
        var settings = Assert.IsType<ConstantRateVideoEncodingSettings>(Assert.Single(settingsResults).BaseObject);
        Assert.Equal("libx264", settings.Codec);
        Assert.Equal(28, settings.CRF);
    }
}
