$dllPath = $null

$debugPath = Join-Path $PSScriptRoot "bin\Debug\net9.0\MediaForgePS.dll"
$releasePath = Join-Path $PSScriptRoot "bin\Release\net9.0\MediaForgePS.dll"

if (Test-Path $releasePath)
{
    $dllPath = $releasePath
}
elseif (Test-Path $debugPath)
{
    $dllPath = $debugPath
}

if ($dllPath)
{
    Import-Module $dllPath
    
    # Initialize dependency injection container
    [Dadstart.Labs.MediaForge.Module.ModuleInitializer]::Initialize() | Out-Null
}

function OnRemove
{
    # Cleanup dependency injection container when module is removed
    [Dadstart.Labs.MediaForge.Module.ModuleInitializer]::Cleanup()
}

$ExecutionContext.SessionState.Module.OnRemove = { OnRemove }
