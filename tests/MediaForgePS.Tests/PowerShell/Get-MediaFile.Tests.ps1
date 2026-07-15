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

Describe 'Get-MediaFile' {
    Context 'Parameter Validation' {
        It 'Should throw error when Path parameter is null' {
            { Get-MediaFile -Path $null -ErrorAction Stop } | Should -Throw
        }

        It 'Should throw error when Path parameter is empty' {
            { Get-MediaFile -Path '' -ErrorAction Stop } | Should -Throw
        }

        It 'Should throw error when Path parameter is whitespace' {
            { Get-MediaFile -Path '   ' -ErrorAction Stop -WarningAction SilentlyContinue } | Should -Throw
        }

        It 'Should accept Path from pipeline' {
            $nonExistentPath = 'NonExistentFile.mp4'
            { $nonExistentPath | Get-MediaFile -ErrorAction Stop -WarningAction SilentlyContinue } | Should -Throw
        }

        It 'Should accept Path from pipeline by property name' {
            $obj = [PSCustomObject]@{ Path = 'NonExistentFile.mp4' }
            { $obj | Get-MediaFile -ErrorAction Stop -WarningAction SilentlyContinue } | Should -Throw
        }
    }

    Context 'File Not Found Handling' {
        It 'Should write error when file does not exist' {
            $nonExistentPath = Join-Path $TestDrive 'NonExistentFile.mp4'
            { Get-MediaFile -Path $nonExistentPath -ErrorAction Stop -WarningAction SilentlyContinue } | Should -Throw
        }
        <#
        TODO: Fix and enable test
        It 'Should write error with correct error category' {
            $nonExistentPath = Join-Path $TestDrive 'NonExistentFile.mp4'
            $errorRecord = $null
            try {
                Get-MediaFile -Path $nonExistentPath -ErrorAction Stop
            }
            catch {
                $errorRecord = $_
            }

            $errorRecord | Should -Not -BeNullOrEmpty
            $errorRecord.CategoryInfo.Category | Should -Be 'ObjectNotFound'
        }
        #>
    }

    Context 'Cmdlet Structure' {
        It 'Should have correct verb and noun' {
            $cmdlet = Get-Command Get-MediaFile
            $cmdlet | Should -Not -BeNullOrEmpty
            $cmdlet.Verb | Should -Be 'Get'
            $cmdlet.Noun | Should -Be 'MediaFile'
        }

        It 'Should have Path parameter with correct attributes' {
            $parameter = (Get-Command Get-MediaFile).Parameters['Path']
            $parameter | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [Parameter] -and $_.Mandatory } | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [Parameter] -and $_.ValueFromPipeline } | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [Parameter] -and $_.ValueFromPipelineByPropertyName } | Should -Not -BeNullOrEmpty
        }

        It 'Should output MediaFile type when successful' {
            $command = Get-Command Get-MediaFile
            $typeNames = @($command.OutputType | ForEach-Object { $_.Name })
            $typeNames | Should -Contain 'Dadstart.Labs.MediaForge.Models.MediaFile'
        }
    }

    <#
    TODO: Fix and re-enable Pipeline Support tests for Get-MediaFile.
    Context 'Pipeline Support' {
        It 'Should process multiple paths from pipeline' {
            $paths = @(
                (Join-Path $TestDrive 'File1.mp4'),
                (Join-Path $TestDrive 'File2.mkv')
            )

            $errors = @()
            $paths | Get-MediaFile -ErrorAction SilentlyContinue -ErrorVariable +errors
            $errors.Count | Should -BeGreaterThan 0
        }

        It 'Should process objects with Path property from pipeline' {
            $objects = @(
                [PSCustomObject]@{ Path = Join-Path $TestDrive 'File1.mp4' },
                [PSCustomObject]@{ Path = Join-Path $TestDrive 'File2.mkv' }
            )

            $errors = @()
            $objects | Get-MediaFile -ErrorAction SilentlyContinue -ErrorVariable +errors
            $errors.Count | Should -BeGreaterThan 0
        }
    }
    #>
}

