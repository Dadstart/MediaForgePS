@{
    RootModule           = 'MediaForgePS.psm1'
    ModuleVersion        = '0.18.0'
    GUID                 = 'a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d'
    Author               = 'Dadstart LLC'
    CompanyName          = 'Dadstart LLC'
    Copyright            = '(c) Dadstart. All rights reserved.'
    Description          = 'PowerShell module for managing video files (MP4, MKV, etc.) directly from the terminal or other scripts. Includes a Media PSProvider for browsing streams and custom formatting for media types.'
    PowerShellVersion    = '7.6'
    CompatiblePSEditions = @('Core')

    # Binary assembly loaded before the root script module (DI bootstrap in MediaForgePS.psm1).
    RequiredAssemblies   = @('MediaForgePS.dll')
    FormatsToProcess     = @('Formats/MediaForgePS.format.ps1xml')

    CmdletsToExport      = @(
        'Get-MediaFile'
        'Get-AudioStreams'
        'Convert-MediaFiles'
        'Convert-MediaFileAdvanced'
        'Convert-VideoFile'
        'Export-MediaStream'
        'Export-Subtitles'
        'Convert-ImageSubtitlesToSrt'
        'Repair-Subtitles'
        'Invoke-SubtitleOcrRepair'
        'Split-Chapters'
        'Split-SeriesChapters'
        'Invoke-SeasonScan'
        'Invoke-VideoCopy'
        'Invoke-SeriesProcessing'
        'Invoke-BonusFileProcessing'
        'New-VideoEncodingSettings'
        'New-AudioTrackMapping'
    )
    FunctionsToExport    = @()
    VariablesToExport    = @()
    AliasesToExport      = @()

    FileList             = @(
        'MediaForgePS.psd1'
        'MediaForgePS.psm1'
        'MediaForgePS.dll'
        'Formats/MediaForgePS.format.ps1xml'
        'en-US/MediaForgePS.dll-Help.xml'
    )

    PrivateData          = @{
        PSData = @{
            Tags         = @('Media', 'Video', 'FFmpeg', 'Ffprobe', 'MediaForge', 'PowerShell')
            LicenseUri   = 'https://github.com/Dadstart/MediaForgePS/blob/main/LICENSE'
            ProjectUri   = 'https://github.com/Dadstart/MediaForgePS'
            ReleaseNotes = 'https://github.com/Dadstart/MediaForgePS/releases'
        }
    }
}
