@{
    RootModule = 'MediaForgePS.psm1'
    ModuleVersion = '0.1.0'
    CompatiblePSEditions = @('Core')
    GUID = 'a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d'
    Author = 'Dadstart Labs'
    CompanyName = 'Dadstart Labs'
    Copyright = '(c) Dadstart Labs. All rights reserved.'
    Description = 'PowerShell module for managing video files (MP4, MKV, etc.) directly from the terminal or other scripts.'
    PowerShellVersion = '7.5'
    DotNetFrameworkVersion = '9.0'
    CmdletsToExport = @(
        'Add-Number',
        'Subtract-Number'
    )
    FunctionsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('Media', 'Video', 'FFmpeg', 'FFprobe')
            LicenseUri = ''
            ProjectUri = ''
            IconUri = ''
            ReleaseNotes = ''
        }
    }
}
