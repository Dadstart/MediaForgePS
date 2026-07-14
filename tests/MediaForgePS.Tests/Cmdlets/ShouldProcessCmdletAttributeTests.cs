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
    [InlineData(typeof(InvokeBonusFileProcessingCommand), ConfirmImpact.High)]
    [InlineData(typeof(ExportMediaStreamCommand), ConfirmImpact.Medium)]
    public void DestructiveCmdlets_DeclareSupportsShouldProcess(Type cmdletType, ConfirmImpact expectedImpact)
    {
        var attribute = cmdletType
            .GetCustomAttributes(typeof(CmdletAttribute), inherit: false)
            .Cast<CmdletAttribute>()
            .Single();

        Assert.True(attribute.SupportsShouldProcess);
        Assert.Equal(expectedImpact, attribute.ConfirmImpact);
    }
}
