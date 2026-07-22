using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.E2ETests.Cmdlets;

public class ConvertImageSubtitlesToSrtE2ETests : E2ETestBase
{
    [Fact(Timeout = 180_000)]
    public void PackedModule_ConvertImageSubtitlesToSrt_IsExportedAndRejectsMissingInput()
    {
        using var ps = ImportPackedModule();

        ps.AddCommand("Get-Command").AddParameter("Name", "Convert-ImageSubtitlesToSrt");
        var commandResults = ps.Invoke().ToList();
        Assert.Empty(ps.Streams.Error.ReadAll());
        Assert.Single(commandResults);
        ps.Commands.Clear();

        var missing = Path.Combine(CreateTempDirectory(), "missing.sup");
        ps.AddCommand("Convert-ImageSubtitlesToSrt").AddParameter("InputPath", missing);
        var results = ps.Invoke().Select(p => p.BaseObject).ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.True(errors.Count > 0 || results.Count == 0);
        if (results.Count > 0)
        {
            var result = Assert.IsType<SubtitleProcessingResult>(Assert.Single(results));
            Assert.Equal(0, result.ConvertedCount);
        }
    }

    [Fact(Timeout = 180_000)]
    public void PackedModule_ConvertSupToSrtAlias_Resolves()
    {
        using var ps = ImportPackedModule();

        ps.AddCommand("Get-Command").AddParameter("Name", "Convert-SupToSrt");
        var commandResults = ps.Invoke().ToList();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var command = Assert.Single(commandResults).BaseObject;
        var alias = Assert.IsType<AliasInfo>(command);
        Assert.Equal("Convert-SupToSrt", alias.Name);
        Assert.Equal("Convert-ImageSubtitlesToSrt", alias.ReferencedCommand.Name);
    }
}
