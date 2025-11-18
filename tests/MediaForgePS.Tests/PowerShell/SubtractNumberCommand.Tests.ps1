BeforeAll {
    $repoRoot = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
    $modulePath = Join-Path $repoRoot "src" "MediaForgePS"
    $dllPath = Join-Path $modulePath "bin" "Debug" "net9.0" "MediaForgePS.dll"
    
    # Build the module if DLL doesn't exist
    if (-not (Test-Path $dllPath)) {
        Write-Host "Building MediaForgePS module..." -ForegroundColor Yellow
        Push-Location $repoRoot
        try {
            $buildOutput = & dotnet build src/MediaForgePS/MediaForgePS.csproj -c Debug 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Build output:" -ForegroundColor Red
                Write-Host ($buildOutput -join "`n") -ForegroundColor Red
                throw "Build failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            Pop-Location
        }
    }
    
    # Import the module
    $moduleManifest = Join-Path $modulePath "MediaForgePS.psd1"
    Import-Module $moduleManifest -Force -ErrorAction Stop
}

Describe 'Subtract-Number' {
    It 'Should subtract two numbers correctly' {
        $result = Subtract-Number -Minuend 10 -Subtrahend 3
        $result | Should -Be 7
    }

    It 'Should handle negative results correctly' {
        $result = Subtract-Number -Minuend 5 -Subtrahend 10
        $result | Should -Be -5
    }

    It 'Should subtract zero correctly' {
        $result = Subtract-Number -Minuend 10 -Subtrahend 0
        $result | Should -Be 10
    }

    It 'Should subtract decimal numbers correctly' {
        $result = Subtract-Number -Minuend 10.5 -Subtrahend 3.7
        $result | Should -Be 6.8
    }

    It 'Should accept pipeline input for Minuend' {
        $result = 10 | Subtract-Number -Subtrahend 3
        $result | Should -Be 7
    }
}
