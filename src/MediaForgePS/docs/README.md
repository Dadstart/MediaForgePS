# MediaForgePS help source (platyPS)

This folder holds the **Markdown source** for module help. PowerShell does not use these files directly.

## Cmdlets documented here

| Cmdlet | Topic file |
|--------|------------|
| `Convert-ImageSubtitlesToSrt` | [Convert-ImageSubtitlesToSrt.md](Convert-ImageSubtitlesToSrt.md) |
| `Convert-MediaFileAdvanced` | [Convert-MediaFileAdvanced.md](Convert-MediaFileAdvanced.md) |
| `Convert-MediaFiles` | [Convert-MediaFiles.md](Convert-MediaFiles.md) |
| `Convert-VideoFile` | [Convert-VideoFile.md](Convert-VideoFile.md) |
| `Export-MediaStream` | [Export-MediaStream.md](Export-MediaStream.md) |
| `Export-Subtitles` | [Export-Subtitles.md](Export-Subtitles.md) |
| `Get-AudioStreams` | [Get-AudioStreams.md](Get-AudioStreams.md) |
| `Get-MediaFile` | [Get-MediaFile.md](Get-MediaFile.md) |
| `Invoke-BonusFileProcessing` | [Invoke-BonusFileProcessing.md](Invoke-BonusFileProcessing.md) |
| `Invoke-SeasonScan` | [Invoke-SeasonScan.md](Invoke-SeasonScan.md) |
| `Invoke-SeriesProcessing` | [Invoke-SeriesProcessing.md](Invoke-SeriesProcessing.md) |
| `Invoke-SubtitleOcrRepair` | [Invoke-SubtitleOcrRepair.md](Invoke-SubtitleOcrRepair.md) |
| `Invoke-VideoCopy` | [Invoke-VideoCopy.md](Invoke-VideoCopy.md) |
| `New-AudioTrackMapping` | [New-AudioTrackMapping.md](New-AudioTrackMapping.md) |
| `New-VideoEncodingSettings` | [New-VideoEncodingSettings.md](New-VideoEncodingSettings.md) |
| `Repair-Subtitles` | [Repair-Subtitles.md](Repair-Subtitles.md) |
| `Split-Chapters` | [Split-Chapters.md](Split-Chapters.md) |
| `Split-SeriesChapters` | [Split-SeriesChapters.md](Split-SeriesChapters.md) |

## Workflow

- **Sync parameters and publish MAML:** from repo root:
  ```powershell
  Install-Module platyPS -Scope CurrentUser
  .\scripts\Update-Help.ps1
  ```
  This runs `Update-MarkdownHelp` on existing files, scaffolds any missing cmdlet topics, and writes `en-US\MediaForgePS.dll-Help.xml`.
- **Edit** the `.md` files (synopsis, description, parameters, examples).
- **Regenerate MAML only** (after editing Markdown, without rebuilding metadata):
  ```powershell
  .\scripts\Build-Help.ps1
  ```
- See **docs/platyPS-help-walkthrough.md** at the repo root for the full walkthrough.
