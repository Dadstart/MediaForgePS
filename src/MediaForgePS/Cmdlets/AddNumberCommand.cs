using System.Management.Automation;

namespace Dadstart.Labs.MediaForge.Cmdlets;

[Cmdlet(VerbsCommon.Add, "Number")]
[OutputType(typeof(double))]
public class AddNumberCommand : PSCmdlet
{
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        HelpMessage = "First number to add")]
    public double FirstNumber { get; set; }

    [Parameter(
        Mandatory = true,
        Position = 1,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Second number to add")]
    public double SecondNumber { get; set; }

    protected override void ProcessRecord()
    {
        var result = FirstNumber + SecondNumber;
        WriteObject(result);
    }
}
