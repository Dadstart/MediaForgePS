@{
    Run = @{
        Path = @(
            'PowerShell'
        )
        PassThru = $true
    }
    Output = @{
        Verbosity = 'Detailed'
    }
    TestResult = @{
        Enabled = $true
        OutputPath = 'TestResults/PesterResults.xml'
        OutputFormat = 'NUnitXml'
    }
}
