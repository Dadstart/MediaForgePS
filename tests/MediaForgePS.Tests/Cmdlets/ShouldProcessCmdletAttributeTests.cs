using System;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ShouldProcessCmdletAttributeTests
{
    [Theory]
    [InlineData(typeof(ConvertMediaFilesCommand))]
    [InlineData(typeof(ConvertVideoFileCommand))]
    [InlineData(typeof(ConvertImageSubtitlesToSrtCommand))]
    [InlineData(typeof(ConvertMediaFileAdvancedCommand))]
    [InlineData(typeof(InvokeVideoCopyCommand))]
    [InlineData(typeof(InvokeBonusFileProcessingCommand))]
    [InlineData(typeof(ExportMediaStreamCommand))]
    public void DestructiveCmdlets_DeclareSupportsShouldProcess(Type cmdletType)
    {
        var attribute = cmdletType
            .GetCustomAttributes(typeof(CmdletAttribute), inherit: false)
            .Cast<CmdletAttribute>()
            .Single();

        Assert.True(attribute.SupportsShouldProcess);
    }
}
