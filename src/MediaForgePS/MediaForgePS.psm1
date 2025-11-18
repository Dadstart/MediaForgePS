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
}
