$dllPath = Join-Path $PSScriptRoot 'MediaForgePS.dll'
if (-not (Test-Path $dllPath)) {
    throw "Module not found at $dllPath"
}

# Import the binary module directly (no .psd1 manifest needed)
Import-Module $dllPath

# Initialize dependency injection container
[Dadstart.Labs.MediaForge.Module.ModuleInitializer]::Initialize() | Out-Null

$ExecutionContext.SessionState.Module.OnRemove = {
    [Dadstart.Labs.MediaForge.Module.ModuleInitializer]::Cleanup()
}
