using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class MediaConversionHelperProgressWriteTests
{
    [Fact]
    public void WriteCurrentItemProgress_WithEta_SetsSecondsRemainingAndPreservesStatus()
    {
        using var ps = PowerShellCmdletTestHost.Create<ProgressWriteProbeCmdlet>("Test-ProgressWrite");
        ps.AddCommand("Test-ProgressWrite")
            .AddParameter("WriteCurrentItem")
            .AddParameter("PercentComplete", 42)
            .AddParameter("EtaSeconds", 30.2);

        _ = ps.Invoke();

        Assert.Empty(ps.Streams.Error);
        var record = Assert.Single(ps.Streams.Progress);
        Assert.Equal(ProgressActivityIds.CurrentItem, record.ActivityId);
        Assert.Equal(ProgressActivityIds.Main, record.ParentActivityId);
        Assert.Equal("File Conversion", record.Activity);
        Assert.Equal("Encoding", record.StatusDescription);
        Assert.Equal("out.mp4", record.CurrentOperation);
        Assert.Equal(42, record.PercentComplete);
        Assert.Equal(31, record.SecondsRemaining);
    }

    [Fact]
    public void WriteMainProgress_WithEta_SetsSecondsRemainingAndPreservesStatus()
    {
        using var ps = PowerShellCmdletTestHost.Create<ProgressWriteProbeCmdlet>("Test-ProgressWrite");
        ps.AddCommand("Test-ProgressWrite")
            .AddParameter("WriteMain")
            .AddParameter("PercentComplete", 15)
            .AddParameter("EtaSeconds", 0.1);

        _ = ps.Invoke();

        Assert.Empty(ps.Streams.Error);
        var record = Assert.Single(ps.Streams.Progress);
        Assert.Equal(ProgressActivityIds.Main, record.ActivityId);
        Assert.Equal("Batch Conversion", record.Activity);
        Assert.Equal("Working", record.StatusDescription);
        Assert.Equal(15, record.PercentComplete);
        Assert.Equal(1, record.SecondsRemaining);
    }

    [Fact]
    public void WriteProgress_WithoutEta_DoesNotSetSecondsRemaining()
    {
        using var ps = PowerShellCmdletTestHost.Create<ProgressWriteProbeCmdlet>("Test-ProgressWrite");
        ps.AddCommand("Test-ProgressWrite")
            .AddParameter("WriteCurrentItem")
            .AddParameter("PercentComplete", 10);

        _ = ps.Invoke();

        Assert.Empty(ps.Streams.Error);
        var record = Assert.Single(ps.Streams.Progress);
        Assert.Equal(10, record.PercentComplete);
        Assert.Equal(-1, record.SecondsRemaining);
    }

    [Cmdlet(VerbsDiagnostic.Test, "ProgressWrite")]
    private sealed class ProgressWriteProbeCmdlet : PSCmdlet
    {
        [Parameter]
        public SwitchParameter WriteMain { get; set; }

        [Parameter]
        public SwitchParameter WriteCurrentItem { get; set; }

        [Parameter]
        public int? PercentComplete { get; set; }

        [Parameter]
        public double? EtaSeconds { get; set; }

        protected override void ProcessRecord()
        {
            TimeSpan? eta = EtaSeconds.HasValue ? TimeSpan.FromSeconds(EtaSeconds.Value) : null;

            if (WriteMain)
            {
                MediaConversionHelper.WriteMainProgress(
                    this,
                    "Batch Conversion",
                    "Working",
                    PercentComplete,
                    eta);
            }

            if (WriteCurrentItem)
            {
                MediaConversionHelper.WriteCurrentItemProgress(
                    this,
                    "File Conversion",
                    "Encoding",
                    "out.mp4",
                    PercentComplete,
                    eta);
            }
        }
    }
}
