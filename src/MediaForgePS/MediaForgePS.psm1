$dllPath = Join-Path $PSScriptRoot 'MediaForgePS.dll'
if (-not (Test-Path $dllPath)) {
    throw "Module not found at $dllPath"
}

# Import the binary module (RequiredAssemblies may already have loaded the assembly).
$importedModule = Import-Module $dllPath -PassThru

# Formatting and type XML under the module root and Formats/. Help XML in en-US is discovered by path.
$runtimeXmlDirectories = @(
    $PSScriptRoot
    (Join-Path $PSScriptRoot 'Formats')
)
$importedRuntimeXml = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($directory in $runtimeXmlDirectories) {
    if (-not (Test-Path -LiteralPath $directory)) {
        continue
    }

    $runtimeXmlFiles = if ($directory -eq $PSScriptRoot) {
        Get-ChildItem -LiteralPath $directory -File |
            Where-Object { $_.Name -like '*.format.ps1xml' -or $_.Name -like '*.types.ps1xml' }
    }
    else {
        Get-ChildItem -LiteralPath $directory -File -Recurse -Filter '*.ps1xml'
    }

    foreach ($file in $runtimeXmlFiles) {
        if (-not $importedRuntimeXml.Add($file.FullName)) {
            continue
        }

        if ($file.Name -like '*.types.ps1xml') {
            Update-TypeData -PrependPath $file.FullName
        }
        elseif ($file.Name -like '*.format.ps1xml') {
            Update-FormatData -PrependPath $file.FullName
        }
    }
}

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
