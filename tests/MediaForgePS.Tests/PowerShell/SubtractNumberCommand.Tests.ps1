BeforeAll {
    $repoRoot = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
    $modulePath = Join-Path $repoRoot "src" "MediaForgePS"
    
    # Unload the module if it's already loaded to avoid file locks
    if (Get-Module -Name MediaForgePS -ErrorAction SilentlyContinue) {
        Remove-Module -Name MediaForgePS -Force -ErrorAction SilentlyContinue
    }
    
    # Detect the target framework from the project file
    $projectFile = Join-Path $modulePath "MediaForgePS.csproj"
    $targetFramework = 'net9.0' # Default
    if (Test-Path $projectFile) {
        $projectContent = Get-Content $projectFile -Raw
        if ($projectContent -match '<TargetFramework>([^<]+)</TargetFramework>') {
            $targetFramework = $matches[1].Trim()
        }
    }
    
    $dllPath = Join-Path $modulePath "bin" "Debug" $targetFramework "MediaForgePS.dll"
    
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
