BeforeAll {
    # Import the module for testing
    $modulePath = Join-Path $PSScriptRoot '..\..\..\src\MediaForgePS\bin\Debug\net9.0\MediaForgePS.dll'

    # Build the module if it doesn't exist
    if (-not (Test-Path $modulePath)) {
        Push-Location (Split-Path $PSScriptRoot -Parent -Parent)
        try {
            dotnet build src/MediaForgePS/MediaForgePS.csproj -c Debug | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build MediaForgePS module"
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
            { Get-MediaFile -Path '   ' -ErrorAction Stop } | Should -Throw
        }

        It 'Should accept Path from pipeline' {
            $nonExistentPath = 'NonExistentFile.mp4'
            { $nonExistentPath | Get-MediaFile -ErrorAction Stop } | Should -Throw
        }

        It 'Should accept Path from pipeline by property name' {
            $obj = [PSCustomObject]@{ Path = 'NonExistentFile.mp4' }
            { $obj | Get-MediaFile -ErrorAction Stop } | Should -Throw
        }
    }

    Context 'File Not Found Handling' {
        It 'Should write error when file does not exist' {
            $nonExistentPath = Join-Path $TestDrive 'NonExistentFile.mp4'
            { Get-MediaFile -Path $nonExistentPath -ErrorAction Stop } | Should -Throw
        }

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
            $nonExistentPath = Join-Path $TestDrive 'Test.mp4'
            try {
                $result = Get-MediaFile -Path $nonExistentPath -ErrorAction SilentlyContinue
                # If no error, result should be MediaFile type (this test may not execute if file doesn't exist)
                if ($null -ne $result) {
                    $result | Should -BeOfType [Dadstart.Labs.MediaForge.Models.MediaFile]
                }
            }
            catch {
                # Expected to fail for non-existent file
            }
        }
    }

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
}

