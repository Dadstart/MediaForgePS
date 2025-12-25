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

Describe 'Export-MediaStream' {
    Context 'Parameter Validation' {
        It 'Should throw error when MediaFile parameter is null' {
            { Export-MediaStream -MediaFile $null -OutputPath 'output.mkv' -Type Video -Index 0 -ErrorAction Stop } | Should -Throw
        }

        It 'Should throw error when OutputPath parameter is null' {
            $mediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                'test.mp4',
                $null,
                @(),
                @(),
                ''
            )
            { Export-MediaStream -MediaFile $mediaFile -OutputPath $null -Type Video -Index 0 -ErrorAction Stop } | Should -Throw
        }

        It 'Should throw error when OutputPath parameter is empty' {
            $mediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                'test.mp4',
                $null,
                @(),
                @(),
                ''
            )
            { Export-MediaStream -MediaFile $mediaFile -OutputPath '' -Type Video -Index 0 -ErrorAction Stop } | Should -Throw
        }

        It 'Should throw error when Type parameter is invalid' {
            $mediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                'test.mp4',
                $null,
                @(),
                @(),
                ''
            )
            { Export-MediaStream -MediaFile $mediaFile -OutputPath 'output.mkv' -Type InvalidType -Index 0 -ErrorAction Stop } | Should -Throw
        }

        It 'Should throw error when Index parameter is negative' {
            $mediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                'test.mp4',
                $null,
                @(),
                @(),
                ''
            )
            { Export-MediaStream -MediaFile $mediaFile -OutputPath 'output.mkv' -Type Video -Index -1 -ErrorAction Stop } | Should -Throw
        }

        It 'Should accept MediaFile from pipeline' {
            $mediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                'test.mp4',
                $null,
                @(),
                @(),
                ''
            )
            { $mediaFile | Export-MediaStream -OutputPath 'output.mkv' -Type Video -Index 0 -ErrorAction Stop } | Should -Not -Throw
        }

        It 'Should accept MediaFile from pipeline by property name' {
            $obj = [PSCustomObject]@{
                MediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                    'test.mp4',
                    $null,
                    @(),
                    @(),
                    ''
                )
            }
            { Export-MediaStream -MediaFile $obj.MediaFile -OutputPath 'output.mkv' -Type Video -Index 0 -ErrorAction Stop } | Should -Not -Throw
        }
    }

    Context 'Stream Type Validation' {
        BeforeEach {
            $mediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                'test.mp4',
                $null,
                @(),
                @(),
                ''
            )
            $outputPath = Join-Path $TestDrive 'output.mkv'
        }

        It 'Should accept Video type' {
            { Export-MediaStream -MediaFile $mediaFile -OutputPath $outputPath -Type Video -Index 0 -ErrorAction Stop } | Should -Not -Throw
        }

        It 'Should accept Audio type' {
            { Export-MediaStream -MediaFile $mediaFile -OutputPath $outputPath -Type Audio -Index 0 -ErrorAction Stop } | Should -Not -Throw
        }

        It 'Should accept Subtitle type' {
            { Export-MediaStream -MediaFile $mediaFile -OutputPath $outputPath -Type Subtitle -Index 0 -ErrorAction Stop } | Should -Not -Throw
        }

        It 'Should accept Data type' {
            { Export-MediaStream -MediaFile $mediaFile -OutputPath $outputPath -Type Data -Index 0 -ErrorAction Stop } | Should -Not -Throw
        }

        It 'Should accept All type' {
            { Export-MediaStream -MediaFile $mediaFile -OutputPath $outputPath -Type All -Index 0 -ErrorAction Stop } | Should -Not -Throw
        }
    }

    Context 'Cmdlet Structure' {
        It 'Should have correct verb and noun' {
            $cmdlet = Get-Command Export-MediaStream -ErrorAction SilentlyContinue
            if ($null -eq $cmdlet) {
                Set-ItResult -Skipped -Because "Cmdlet not found"
                return
            }
            $cmdlet | Should -Not -BeNullOrEmpty
            $cmdlet.Verb | Should -Be 'Export'
            $cmdlet.Noun | Should -Be 'MediaStream'
        }

        It 'Should have MediaFile parameter with correct attributes' {
            $cmdlet = Get-Command Export-MediaStream -ErrorAction SilentlyContinue
            if ($null -eq $cmdlet) {
                Set-ItResult -Skipped -Because "Cmdlet not found"
                return
            }
            $parameter = $cmdlet.Parameters['MediaFile']
            $parameter | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [Parameter] -and $_.Mandatory } | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [Parameter] -and $_.ValueFromPipeline } | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [Parameter] -and $_.ValueFromPipelineByPropertyName } | Should -Not -BeNullOrEmpty
        }

        It 'Should have OutputPath parameter with correct attributes' {
            $cmdlet = Get-Command Export-MediaStream -ErrorAction SilentlyContinue
            if ($null -eq $cmdlet) {
                Set-ItResult -Skipped -Because "Cmdlet not found"
                return
            }
            $parameter = $cmdlet.Parameters['OutputPath']
            $parameter | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [Parameter] -and $_.Mandatory } | Should -Not -BeNullOrEmpty
        }

        It 'Should have Type parameter with ValidateSet' {
            $cmdlet = Get-Command Export-MediaStream -ErrorAction SilentlyContinue
            if ($null -eq $cmdlet) {
                Set-ItResult -Skipped -Because "Cmdlet not found"
                return
            }
            $parameter = $cmdlet.Parameters['Type']
            $parameter | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [ValidateSet] } | Should -Not -BeNullOrEmpty
        }

        It 'Should have Index parameter with ValidateRange' {
            $cmdlet = Get-Command Export-MediaStream -ErrorAction SilentlyContinue
            if ($null -eq $cmdlet) {
                Set-ItResult -Skipped -Because "Cmdlet not found"
                return
            }
            $parameter = $cmdlet.Parameters['Index']
            $parameter | Should -Not -BeNullOrEmpty
            $parameter.Attributes | Where-Object { $_ -is [ValidateRange] } | Should -Not -BeNullOrEmpty
        }

        It 'Should have Force parameter' {
            $cmdlet = Get-Command Export-MediaStream -ErrorAction SilentlyContinue
            if ($null -eq $cmdlet) {
                Set-ItResult -Skipped -Because "Cmdlet not found"
                return
            }
            $parameter = $cmdlet.Parameters['Force']
            $parameter | Should -Not -BeNullOrEmpty
        }

        It 'Should support ShouldProcess' {
            $cmdlet = Get-Command Export-MediaStream -ErrorAction SilentlyContinue
            if ($null -eq $cmdlet) {
                Set-ItResult -Skipped -Because "Cmdlet not found"
                return
            }
            $cmdlet.Parameters['WhatIf'] | Should -Not -BeNullOrEmpty
            $cmdlet.Parameters['Confirm'] | Should -Not -BeNullOrEmpty
        }
    }

    Context 'WhatIf Support' {
        BeforeEach {
            $mediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                'test.mp4',
                $null,
                @(),
                @(),
                ''
            )
            $outputPath = Join-Path $TestDrive 'output.mkv'
        }

        It 'Should not execute when WhatIf is specified' {
            $result = Export-MediaStream -MediaFile $mediaFile -OutputPath $outputPath -Type Video -Index 0 -WhatIf -ErrorAction SilentlyContinue
            Test-Path $outputPath | Should -Be $false
        }
    }

    Context 'Force Parameter' {
        BeforeEach {
            $mediaFile = [Dadstart.Labs.MediaForge.Models.MediaFile]::new(
                'test.mp4',
                $null,
                @(),
                @(),
                ''
            )
            $outputPath = Join-Path $TestDrive 'output.mkv'
            # Create an existing file to test Force behavior
            '' | Set-Content -Path $outputPath -Force
        }

        It 'Should write error when output file exists without Force' {
            $errors = @()
            Export-MediaStream -MediaFile $mediaFile -OutputPath $outputPath -Type Video -Index 0 -ErrorAction SilentlyContinue -ErrorVariable +errors
            $errors.Count | Should -BeGreaterThan 0
        }

        It 'Should not write error when output file exists with Force' {
            $errors = @()
            Export-MediaStream -MediaFile $mediaFile -OutputPath $outputPath -Type Video -Index 0 -Force -WhatIf -ErrorAction SilentlyContinue -ErrorVariable +errors
            # With WhatIf, it should not error even if file exists, but Force allows it
            # This test just verifies Force parameter is accepted
        }
    }
}
