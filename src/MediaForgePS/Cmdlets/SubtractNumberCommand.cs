using System.Management.Automation;

namespace Dadstart.Labs.MediaForge.Cmdlets;

[Cmdlet("Subtract", "Number")]
[OutputType(typeof(double))]
public class SubtractNumberCommand : PSCmdlet
{
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        HelpMessage = "Number to subtract from")]
    public double Minuend { get; set; }

    [Parameter(
        Mandatory = true,
        Position = 1,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Number to subtract")]
    public double Subtrahend { get; set; }

    protected override void ProcessRecord()
    {
        var result = Minuend - Subtrahend;
        WriteObject(result);
    }
}
