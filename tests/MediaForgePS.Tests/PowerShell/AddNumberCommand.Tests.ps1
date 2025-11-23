BeforeAll {
    $repoRoot = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
    $modulePath = Join-Path $repoRoot "src" "MediaForgePS"
    $dllPath = Join-Path $modulePath "bin" "Debug" "net10.0" "MediaForgePS.dll"
    
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

Describe 'Add-Number' {
    It 'Should add two positive numbers correctly' {
        $result = Add-Number -FirstNumber 5 -SecondNumber 3
        $result | Should -Be 8
    }

    It 'Should add negative numbers correctly' {
        $result = Add-Number -FirstNumber -5 -SecondNumber -3
        $result | Should -Be -8
    }

    It 'Should add zero correctly' {
        $result = Add-Number -FirstNumber 10 -SecondNumber 0
        $result | Should -Be 10
    }

    It 'Should add decimal numbers correctly' {
        $result = Add-Number -FirstNumber 5.5 -SecondNumber 3.7
        $result | Should -Be 9.2
    }

    It 'Should accept pipeline input for FirstNumber' {
        $result = 5 | Add-Number -SecondNumber 3
        $result | Should -Be 8
    }
}
