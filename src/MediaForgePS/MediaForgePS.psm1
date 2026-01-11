$dllPath = Join-Path $PSScriptRoot 'MediaForgePS.dll'
if (-not (Test-Path $dllPath)) {
    throw "Module not found at $dllPath"
}

# Import the binary module directly (no .psd1 manifest needed)
$importedModule = Import-Module $dllPath -PassThru

# Initialize dependency injection container
[Dadstart.Labs.MediaForge.Module.ModuleInitializer]::Initialize() | Out-Null

# Export all cmdlets from the imported binary module
$cmdlets = $importedModule.ExportedCmdlets.Values.Name
if ($cmdlets) {
    Export-ModuleMember -Cmdlet $cmdlets
}

$ExecutionContext.SessionState.Module.OnRemove = {
    [Dadstart.Labs.MediaForge.Module.ModuleInitializer]::Cleanup()
}
