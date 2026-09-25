using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class CmdletBaseTerminalTitleTests
{
    [Theory]
    [InlineData("", null, "MF: Other")]
    [InlineData("Convert-VideoFile", null, "MF: Convert-VideoFile")]
    [InlineData("Convert-VideoFile", "Encoding", "MF: Convert-VideoFile: Encoding")]
    public void BuildTerminalTitle_ReturnsExpectedText(string commandName, string? operationName, string expected)
    {
        var actual = TerminalTitleProbeCmdlet.FormatTerminalTitle(commandName, operationName);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, "MF: Convert-VideoFile")]
    [InlineData(-1, "MF: Convert-VideoFile")]
    [InlineData(0, "0% MF: Convert-VideoFile")]
    [InlineData(42, "42% MF: Convert-VideoFile")]
    [InlineData(100, "100% MF: Convert-VideoFile")]
    [InlineData(101, "MF: Convert-VideoFile")]
    public void Format_PrefixesActivePercent(int? percent, string expected)
    {
        var actual = ProgressTerminalTitle.Format("MF: Convert-VideoFile", percent);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Apply_UsesCurrentItemPercentWhileItIsActive()
    {
        var title = new ProgressTerminalTitle("MF: Convert-MediaFiles");

        var afterMain = title.Apply(CreateProgress(ProgressActivityIds.Main, 20));
        var afterItem = title.Apply(CreateProgress(ProgressActivityIds.CurrentItem, 75));
        var afterItemCompleted = title.Apply(CreateCompleted(ProgressActivityIds.CurrentItem));
        var afterMainCompleted = title.Apply(CreateCompleted(ProgressActivityIds.Main));

        Assert.Equal("20% MF: Convert-MediaFiles", afterMain);
        Assert.Equal("75% MF: Convert-MediaFiles", afterItem);
        Assert.Equal("20% MF: Convert-MediaFiles", afterItemCompleted);
        Assert.Equal("MF: Convert-MediaFiles", afterMainCompleted);
    }

    [Fact]
    public void Apply_CompletedMainClearsCurrentItemPercent()
    {
        var title = new ProgressTerminalTitle("MF: Repair-Subtitles");

        title.Apply(CreateProgress(ProgressActivityIds.Main, 40));
        title.Apply(CreateProgress(ProgressActivityIds.CurrentItem, 90));
        var afterMainCompleted = title.Apply(CreateCompleted(ProgressActivityIds.Main));

        Assert.Equal("MF: Repair-Subtitles", afterMainCompleted);
    }

    [Fact]
    public void Progress_WhenWindowTitleSupported_PrefixesPercentAndRestoresTitle()
    {
        var host = new TitleCapturePsHost("Original Title");
        using var hosted = CreateHostedPowerShell<ProgressTitleProbeCmdlet>(host, "Invoke-ProgressTitleProbe");
        var ps = hosted.PowerShell;

        ProgressTitleProbeCmdlet.Reset();
        ps.AddCommand("Invoke-ProgressTitleProbe");
        _ = ps.Invoke().ToList();

        Assert.Empty(ps.Streams.Error.ReadAll());
        Assert.Equal("20% MF: Invoke-ProgressTitleProbe", ProgressTitleProbeCmdlet.TitleDuringMainProgress);
        Assert.Equal("75% MF: Invoke-ProgressTitleProbe", ProgressTitleProbeCmdlet.TitleDuringItemProgress);
        Assert.Equal("20% MF: Invoke-ProgressTitleProbe", ProgressTitleProbeCmdlet.TitleAfterItemCompleted);
        Assert.Equal("MF: Invoke-ProgressTitleProbe", ProgressTitleProbeCmdlet.TitleAfterCompleted);
        Assert.Equal("Original Title", host.WindowTitle);
    }

    [Fact]
    public void Progress_WithoutCommandTitle_PrefixesExistingTitleAndRestoresIt()
    {
        var host = new TitleCapturePsHost("Original Title");
        using var hosted = CreateHostedPowerShell<ProgressTitleExistingProbeCmdlet>(host, "Invoke-ProgressTitleExistingProbe");
        var ps = hosted.PowerShell;

        ProgressTitleExistingProbeCmdlet.Reset();
        ps.AddCommand("Invoke-ProgressTitleExistingProbe");
        _ = ps.Invoke().ToList();

        Assert.Empty(ps.Streams.Error.ReadAll());
        Assert.Equal("40% Original Title", ProgressTitleExistingProbeCmdlet.TitleDuringProgress);
        Assert.Equal("Original Title", ProgressTitleExistingProbeCmdlet.TitleAfterProgress);
        Assert.Equal("Original Title", host.WindowTitle);
    }

    [Fact]
    public void LongRunningCmdlets_OverrideTerminalTitleOptIn()
    {
        var longRunningCmdletTypes = new[]
        {
            typeof(ConvertMediaFileAdvancedCommand),
            typeof(ConvertMediaFilesCommand),
            typeof(ConvertVideoFileCommand),
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
    public void ConvertVideoFileCommand_UsesConvertVideoFileName()
    {
        var cmdletAttribute = typeof(ConvertVideoFileCommand).GetCustomAttribute<CmdletAttribute>();

        Assert.NotNull(cmdletAttribute);
        Assert.Equal("Convert", cmdletAttribute!.VerbName);
        Assert.Equal("VideoFile", cmdletAttribute.NounName);
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

    [Cmdlet(VerbsLifecycle.Invoke, "ProgressTitleProbe")]
    private sealed class ProgressTitleProbeCmdlet : CmdletBase
    {
        public static string? TitleDuringMainProgress { get; private set; }

        public static string? TitleDuringItemProgress { get; private set; }

        public static string? TitleAfterItemCompleted { get; private set; }

        public static string? TitleAfterCompleted { get; private set; }

        protected override bool ShouldSetCommandTerminalTitle => true;

        public static void Reset()
        {
            TitleDuringMainProgress = null;
            TitleDuringItemProgress = null;
            TitleAfterItemCompleted = null;
            TitleAfterCompleted = null;
        }

        protected override void Process()
        {
            CmdletIO.WriteProgress(CreateProgress(ProgressActivityIds.Main, 20));
            TitleDuringMainProgress = ReadWindowTitle();

            CmdletIO.WriteProgress(CreateProgress(ProgressActivityIds.CurrentItem, 75));
            TitleDuringItemProgress = ReadWindowTitle();

            CmdletIO.WriteProgress(CreateCompleted(ProgressActivityIds.CurrentItem));
            TitleAfterItemCompleted = ReadWindowTitle();

            CmdletIO.WriteProgress(CreateCompleted(ProgressActivityIds.Main));
            TitleAfterCompleted = ReadWindowTitle();
        }

        private string? ReadWindowTitle()
        {
            try
            {
                return Host.UI.RawUI.WindowTitle;
            }
            catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
            {
                return null;
            }
        }
    }

    [Cmdlet(VerbsLifecycle.Invoke, "ProgressTitleExistingProbe")]
    private sealed class ProgressTitleExistingProbeCmdlet : CmdletBase
    {
        public static string? TitleDuringProgress { get; private set; }

        public static string? TitleAfterProgress { get; private set; }

        public static void Reset()
        {
            TitleDuringProgress = null;
            TitleAfterProgress = null;
        }

        protected override void Process()
        {
            CmdletIO.WriteProgress(CreateProgress(ProgressActivityIds.Main, 40));
            TitleDuringProgress = ReadWindowTitle();

            CmdletIO.WriteProgress(CreateCompleted(ProgressActivityIds.Main));
            TitleAfterProgress = ReadWindowTitle();
        }

        private string? ReadWindowTitle()
        {
            try
            {
                return Host.UI.RawUI.WindowTitle;
            }
            catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
            {
                return null;
            }
        }
    }

    private static HostedPowerShell CreateHostedPowerShell<TCmdlet>(TitleCapturePsHost host, string commandName)
        where TCmdlet : CmdletBase
    {
        var asm = typeof(TCmdlet).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName!, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry(commandName, typeof(TCmdlet), null));
        var runspace = RunspaceFactory.CreateRunspace(host, initialSessionState);
        runspace.Open();
        var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;
        return new HostedPowerShell(powerShell, runspace);
    }

    private sealed class HostedPowerShell : IDisposable
    {
        private readonly Runspace _runspace;

        public HostedPowerShell(PowerShell powerShell, Runspace runspace)
        {
            PowerShell = powerShell;
            _runspace = runspace;
        }

        public PowerShell PowerShell { get; }

        public void Dispose()
        {
            PowerShell.Dispose();
            _runspace.Dispose();
        }
    }

    private static ProgressRecord CreateProgress(int activityId, int percent) =>
        new(activityId, "Work", "Working")
        {
            PercentComplete = percent
        };

    private static ProgressRecord CreateCompleted(int activityId) =>
        new(activityId, "Work", "Completed")
        {
            RecordType = ProgressRecordType.Completed
        };
}
