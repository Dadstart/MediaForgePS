using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Dadstart.Labs.MediaForge.Tests.TestInfrastructure;

/// <summary>
/// Creates isolated PowerShell instances with test cmdlets loaded.
/// </summary>
public static class PowerShellCmdletTestHost
{
    public static PowerShell Create<TCmdlet>(string commandName)
    {
        var asm = typeof(TCmdlet).Assembly;
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Assemblies.Add(new SessionStateAssemblyEntry(asm.GetName().FullName!, asm.Location));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry(commandName, typeof(TCmdlet), null));
        return PowerShell.Create(initialSessionState);
    }
}
