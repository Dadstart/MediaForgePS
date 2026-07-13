using System;
using System.Management.Automation;
using System.Reflection;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public sealed class ProgressCmdletAlertTests
{
    private static readonly Type[] _progressCmdletTypes =
    [
        typeof(ConvertImageSubtitlesToSrtCommand),
        typeof(ConvertMediaFilesCommand),
        typeof(ConvertVideoFileCommand),
        typeof(ExportSubtitlesCommand),
        typeof(InvokeBonusFileProcessingCommand),
        typeof(InvokeSeriesProcessingCommand),
        typeof(InvokeSubtitleOcrRepairCommand),
        typeof(InvokeVideoCopyCommand),
        typeof(RepairSubtitlesCommand)
    ];

    [Fact]
    public void ProgressCmdlets_InheritProgressCmdletBase()
    {
        foreach (var cmdletType in _progressCmdletTypes)
            Assert.True(
                typeof(ProgressCmdletBase).IsAssignableFrom(cmdletType),
                $"{cmdletType.Name} should inherit ProgressCmdletBase");
    }

    [Fact]
    public void ProgressCmdlets_ExposeNoAlertSwitchParameter()
    {
        foreach (var cmdletType in _progressCmdletTypes)
        {
            var property = cmdletType.GetProperty(nameof(ProgressCmdletBase.NoAlert), BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            Assert.Equal(typeof(SwitchParameter), property!.PropertyType);

            var parameter = property.GetCustomAttribute<ParameterAttribute>();
            Assert.NotNull(parameter);
        }
    }

    [Fact]
    public void NonProgressCmdlets_DoNotInheritProgressCmdletBase()
    {
        var nonProgressCmdletTypes = new[]
        {
            typeof(ConvertMediaFileAdvancedCommand),
            typeof(ExportMediaStreamCommand),
            typeof(GetMediaFileCommand),
            typeof(SplitChaptersCommand),
            typeof(SplitSeriesChaptersCommand)
        };

        foreach (var cmdletType in nonProgressCmdletTypes)
            Assert.False(
                typeof(ProgressCmdletBase).IsAssignableFrom(cmdletType),
                $"{cmdletType.Name} should not inherit ProgressCmdletBase");
    }

    [Fact]
    public void Alert_WhenNoAlertNotSet_PlaysCompletionAlertOnce()
    {
        lock (AlertProbeCmdlet.SyncRoot)
        {
            AlertProbeCmdlet.Reset();
            using var ps = PowerShellCmdletTestHost.Create<AlertProbeCmdlet>("Invoke-AlertProbe");
            ps.AddCommand("Invoke-AlertProbe");

            _ = ps.Invoke();

            Assert.Empty(ps.Streams.Error);
            Assert.Equal(1, AlertProbeCmdlet.PlayCount);
        }
    }

    [Fact]
    public void Alert_WhenNoAlertSet_DoesNotPlayCompletionAlert()
    {
        lock (AlertProbeCmdlet.SyncRoot)
        {
            AlertProbeCmdlet.Reset();
            using var ps = PowerShellCmdletTestHost.Create<AlertProbeCmdlet>("Invoke-AlertProbe");
            ps.AddCommand("Invoke-AlertProbe").AddParameter("NoAlert");

            _ = ps.Invoke();

            Assert.Empty(ps.Streams.Error);
            Assert.Equal(0, AlertProbeCmdlet.PlayCount);
        }
    }

    [Cmdlet(VerbsLifecycle.Invoke, "AlertProbe")]
    private sealed class AlertProbeCmdlet : ProgressCmdletBase
    {
        public static readonly object SyncRoot = new();

        public static int PlayCount { get; private set; }

        public static void Reset() => PlayCount = 0;

        protected override void PlayCompletionAlert()
        {
            PlayCount++;
        }
    }
}
