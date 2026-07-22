$dllPath = Join-Path $PSScriptRoot 'MediaForgePS.dll'
if (-not (Test-Path $dllPath)) {
    throw "Module not found at $dllPath"
}

# Import the binary module (RequiredAssemblies may already have loaded the assembly).
$importedModule = Import-Module $dllPath -PassThru

# Initialize dependency injection container
[Dadstart.Labs.MediaForge.Module.ModuleInitializer]::Initialize() | Out-Null

# Export all cmdlets and aliases from the imported binary module
$cmdlets = $importedModule.ExportedCmdlets.Values.Name
$aliases = $importedModule.ExportedAliases.Values.Name
if ($cmdlets -or $aliases) {
    Export-ModuleMember -Cmdlet $cmdlets -Alias $aliases
}

$ExecutionContext.SessionState.Module.OnRemove = {
    [Dadstart.Labs.MediaForge.Module.ModuleInitializer]::Cleanup()
}
