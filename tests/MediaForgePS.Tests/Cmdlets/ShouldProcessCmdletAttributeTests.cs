using System;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ShouldProcessCmdletAttributeTests
{
    [Theory]
    [InlineData(typeof(ConvertMediaFilesCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(ConvertVideoFileCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(ConvertImageSubtitlesToSrtCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(ConvertMediaFileAdvancedCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(InvokeVideoCopyCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(InvokeBonusFileProcessingCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(ExportMediaStreamCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(InvokeSeriesProcessingCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(SplitChaptersCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(SplitSeriesChaptersCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(ExportSubtitlesCommand), ConfirmImpact.Medium)]
    [InlineData(typeof(RepairSubtitlesCommand), ConfirmImpact.High)]
    [InlineData(typeof(InvokeSubtitleOcrRepairCommand), ConfirmImpact.High)]
    public void DestructiveCmdlets_DeclareSupportsShouldProcess(Type cmdletType, ConfirmImpact expectedConfirmImpact)
    {
        var attribute = cmdletType
            .GetCustomAttributes(typeof(CmdletAttribute), inherit: false)
            .Cast<CmdletAttribute>()
            .Single();

        Assert.True(attribute.SupportsShouldProcess);
        Assert.Equal(expectedConfirmImpact, attribute.ConfirmImpact);
    }
}
