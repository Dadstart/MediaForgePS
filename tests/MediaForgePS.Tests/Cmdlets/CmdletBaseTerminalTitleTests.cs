using System;
using System.Management.Automation;
using System.Reflection;
using Dadstart.Labs.MediaForge.Cmdlets;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class CmdletBaseTerminalTitleTests
{
    [Theory]
    [InlineData("", null, "MF: Other")]
    [InlineData("Convert-MkvDirectory", null, "MF: Convert-MkvDirectory")]
    [InlineData("Convert-MkvDirectory", "Encoding", "MF: Convert-MkvDirectory: Encoding")]
    public void BuildTerminalTitle_ReturnsExpectedText(string commandName, string? operationName, string expected)
    {
        var actual = TerminalTitleProbeCmdlet.FormatTerminalTitle(commandName, operationName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LongRunningCmdlets_OverrideTerminalTitleOptIn()
    {
        var longRunningCmdletTypes = new[]
        {
            typeof(ConvertMediaFileAdvancedCommand),
            typeof(ConvertMediaFilesCommand),
            typeof(ConvertMkvDirectoryCommand),
            typeof(ExportMediaStreamCommand),
            typeof(ExportSubtitlesCommand),
            typeof(InvokeBonusFileProcessingCommand),
            typeof(InvokeSeriesProcessingCommand),
            typeof(SplitChaptersCommand),
            typeof(SplitSeriesChaptersCommand)
        };

        foreach (var cmdletType in longRunningCmdletTypes)
            AssertTerminalTitleOptIn(cmdletType, expectedOptIn: true);
    }

    [Fact]
    public void ShortRunningCmdlets_DoNotOverrideTerminalTitleOptIn()
    {
        AssertTerminalTitleOptIn(typeof(GetMediaFileCommand), expectedOptIn: false);
    }

    [Fact]
    public void ConvertMkvDirectoryCommand_UsesConvertVideoFileName_WithLegacyAlias()
    {
        var cmdletAttribute = typeof(ConvertMkvDirectoryCommand).GetCustomAttribute<CmdletAttribute>();
        var aliasAttribute = typeof(ConvertMkvDirectoryCommand).GetCustomAttribute<AliasAttribute>();

        Assert.NotNull(cmdletAttribute);
        Assert.Equal("Convert", cmdletAttribute!.VerbName);
        Assert.Equal("VideoFile", cmdletAttribute.NounName);

        Assert.NotNull(aliasAttribute);
        Assert.Contains("Convert-MkvDirectory", aliasAttribute!.AliasNames, StringComparer.Ordinal);
    }

    private static void AssertTerminalTitleOptIn(Type cmdletType, bool expectedOptIn)
    {
        var property = cmdletType.GetProperty(
            "ShouldSetCommandTerminalTitle",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        var getter = property!.GetMethod;
        Assert.NotNull(getter);

        var isOverridden = getter!.DeclaringType != typeof(CmdletBase);
        Assert.Equal(expectedOptIn, isOverridden);
    }

    [Cmdlet(VerbsLifecycle.Invoke, "TerminalTitleProbe")]
    private sealed class TerminalTitleProbeCmdlet : CmdletBase
    {
        public static string FormatTerminalTitle(string commandName, string? operationName)
        {
            return BuildTerminalTitle(commandName, operationName);
        }
    }
}
