BeforeAll {
    $repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $configuration = $env:MEDIAFORGE_CONFIGURATION
    if ([string]::IsNullOrWhiteSpace($configuration)) {
        $configuration = 'Debug'
    }

    $devToolsPath = Join-Path $repoRoot 'scripts\MediaForge.DevTools.psm1'
    if (Test-Path $devToolsPath) {
        Import-Module $devToolsPath -Force
        $moduleDir = Get-MediaForgeBuildOutput -RepoRoot $repoRoot -Configuration $configuration
        $modulePath = Join-Path $moduleDir 'MediaForgePS.dll'
    } else {
        $modulePath = Join-Path $PSScriptRoot "..\..\..\src\MediaForgePS\bin\$configuration\net10.0\MediaForgePS.dll"
    }

    if (-not (Test-Path $modulePath)) {
        Push-Location $repoRoot
        try {
            dotnet build src/MediaForgePS/MediaForgePS.csproj -c $configuration | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build MediaForgePS module ($configuration)"
            }
        }
        finally {
            Pop-Location
        }
    }

    Import-Module $modulePath -Force
}

AfterAll {
    . $PSScriptRoot\TestHelpers.ps1
    Remove-MediaForgePSModule
}

Describe 'Convert-ImageSubtitlesToSrt' {
    Context 'Parameter Validation' {
        It 'Should throw when InputPath is null' {
            { Convert-ImageSubtitlesToSrt -InputPath $null -ErrorAction Stop } | Should -Throw
        }

        It 'Should warn when InputPath is whitespace-only' {
            $null = Convert-ImageSubtitlesToSrt -InputPath @('   ') -WarningVariable warnings -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
            $warnings | Should -Not -BeNullOrEmpty
            $warnings[0].Message | Should -Match 'No input path'
        }

        It 'Should accept Convert-SupToSrt alias' {
            Get-Alias Convert-SupToSrt | Select-Object -ExpandProperty Definition | Should -Be 'Convert-ImageSubtitlesToSrt'
        }
    }

    Context 'Missing input handling' {
        It 'Should write an error when the input file does not exist' {
            $missing = Join-Path $TestDrive 'missing.sup'
            $null = Convert-ImageSubtitlesToSrt -InputPath $missing -ErrorAction SilentlyContinue -ErrorVariable errors
            $errors | Should -Not -BeNullOrEmpty
        }
    }
}
