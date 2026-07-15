# MediaForgePS cmdlet documentation

This folder holds the **Markdown source** for module help. PowerShell does not read these files directly; `Get-Help` loads the generated MAML file `en-US\MediaForgePS.dll-Help.xml`.

## Quick start

```powershell
# From repo root — build, sync parameters, regenerate MAML
.\scripts\Update-Help.ps1

# Import and read help
Import-Module .\src\MediaForgePS\bin\Debug\net10.0\MediaForgePS.psd1 -Force
Get-Help Convert-VideoFile -Full
```

See [docs/platyPS-help-walkthrough.md](../../../docs/platyPS-help-walkthrough.md) for the full platyPS authoring workflow.

## Cmdlet index

### Media inspection

| Cmdlet | Topic | Purpose |
|--------|-------|---------|
| `Get-MediaFile` | [Get-MediaFile.md](Get-MediaFile.md) | ffprobe metadata: format, streams, chapters |
| `Get-AudioStreams` | [Get-AudioStreams.md](Get-AudioStreams.md) | English audio mappings for conversion |
| `Export-MediaStream` | [Export-MediaStream.md](Export-MediaStream.md) | Extract one stream without re-encoding |

### Conversion

| Cmdlet | Topic | Purpose |
|--------|-------|---------|
| `Convert-VideoFile` | [Convert-VideoFile.md](Convert-VideoFile.md) | Directory-oriented batch MP4 conversion + captions |
| `Convert-MediaFiles` | [Convert-MediaFiles.md](Convert-MediaFiles.md) | Flexible batch conversion with encoder presets |
| `Convert-MediaFileAdvanced` | [Convert-MediaFileAdvanced.md](Convert-MediaFileAdvanced.md) | Single-file conversion with explicit settings |
| `New-VideoEncodingSettings` | [New-VideoEncodingSettings.md](New-VideoEncodingSettings.md) | Build video encoding settings objects |
| `New-AudioTrackMapping` | [New-AudioTrackMapping.md](New-AudioTrackMapping.md) | Build copy/encode audio mapping objects |

### Subtitles

| Cmdlet | Topic | Purpose |
|--------|-------|---------|
| `Export-Subtitles` | [Export-Subtitles.md](Export-Subtitles.md) | Extract English subtitles; optional OCR (alias: `Export-RepairedSubtitles`) |
| `Convert-ImageSubtitlesToSrt` | [Convert-ImageSubtitlesToSrt.md](Convert-ImageSubtitlesToSrt.md) | SUP/SUB → SRT (alias: `Convert-SupToSrt`) |
| `Repair-Subtitles` | [Repair-Subtitles.md](Repair-Subtitles.md) | Fix common OCR errors in SRT files |
| `Invoke-SubtitleOcrRepair` | [Invoke-SubtitleOcrRepair.md](Invoke-SubtitleOcrRepair.md) | OCR image subtitles, then repair converted SRT |

### Chapters and TV workflows

| Cmdlet | Topic | Purpose |
|--------|-------|---------|
| `Split-Chapters` | [Split-Chapters.md](Split-Chapters.md) | Split by chapter ranges |
| `Split-SeriesChapters` | [Split-SeriesChapters.md](Split-SeriesChapters.md) | Split with TVDb episode naming |
| `Invoke-SeasonScan` | [Invoke-SeasonScan.md](Invoke-SeasonScan.md) | Fetch TVDb episode metadata |
| `Invoke-VideoCopy` | [Invoke-VideoCopy.md](Invoke-VideoCopy.md) | Copy episodes with TVDb naming |
| `Invoke-SeriesProcessing` | [Invoke-SeriesProcessing.md](Invoke-SeriesProcessing.md) | Full season workflow |
| `Invoke-BonusFileProcessing` | [Invoke-BonusFileProcessing.md](Invoke-BonusFileProcessing.md) | Plex bonus content conversion |

## Common workflows

### Inspect before converting

```powershell
$media = Get-MediaFile ".\episode.mkv"
$media.Streams | Format-Table Index, Type, Codec, Language
$mappings = Get-AudioStreams -InputPath $media.Path
```

### Convert a video library

**Simple (recommended for folders):**

```powershell
Convert-VideoFile -InputPath "C:\Videos" -OutputDirectory "C:\Out" -Recurse -DefaultVideoEncoder nvenc
```

**Batch with explicit file list:**

```powershell
Get-ChildItem "C:\Videos" -Filter *.mkv | Convert-MediaFiles -OutputDirectory "C:\Out" -DefaultVideoEncoder x265
```

**Single file, full control:**

```powershell
$settings = New-VideoEncodingSettings -Codec libx265 -CRF 20 -Preset slow
$mappings = Get-AudioStreams -InputPath "C:\movie.mkv"
Convert-MediaFileAdvanced -InputPath "C:\movie.mkv" -OutputPath "C:\Out\movie.mp4" `
    -VideoEncodingSettings $settings -AudioTrackMappings $mappings
```

### Subtitle extraction and OCR

`-Ocr` accepts **Auto** (default), **Skip**, or **Force**:

| Value | Behavior |
|-------|----------|
| `Auto` | OCR image subtitles when the source has one exported subtitle format and it is not SRT; unused VobSub/SUP sidecars left beside a text SRT are deleted unless `-KeepSource` |
| `Force` | OCR all exported image subtitle files |
| `Skip` | Extract only; no OCR or repair |

```powershell
# During video conversion
Convert-VideoFile -InputPath "C:\In" -Ocr Auto

# Standalone subtitle export
Export-Subtitles -InputPath "C:\Videos\movie.mkv" -Ocr Force -BackupPath "C:\Backup\srts"

# OCR files already on disk
Invoke-SubtitleOcrRepair -InputPath "C:\Subs" -Recurse
```

### TV season processing

```powershell
Invoke-SeriesProcessing `
    -Title "My Show" -Season 1 `
    -InputPath "C:\Source" -FilePatterns "*.mkv" `
    -OutputPath "P:\TV" `
    -TvDbSeriesUrl "https://thetvdb.com/series/12345" `
    -ExtractChapters -Ocr Auto
```

Lower-level steps (for custom automation):

```powershell
$episodes = Invoke-SeasonScan -Season 1 -TvDbSeriesUrl "https://thetvdb.com/series/12345"
Invoke-VideoCopy -Title "My Show" -Season 1 -Path "C:\Source" -FilePatterns "*.mkv" `
    -Destination "P:\TV\My Show\Season 01" -Episodes $episodes
```

## External dependencies

| Tool | Required by |
|------|-------------|
| `ffmpeg`, `ffprobe` | Most cmdlets |
| `mkvextract` (mkvtoolnix) | Matroska VobSub extraction |
| Subtitle Edit + Tesseract | Image subtitle OCR (`-Ocr Auto` or `Force`) |
| TVDb (network) | `Invoke-SeasonScan`, TV naming cmdlets |

## Maintaining help

1. Edit the `.md` files in this folder (synopsis, description, parameters, examples).
2. Run `.\scripts\Update-Help.ps1` from the repo root to sync parameter metadata and regenerate MAML.
3. Or run `.\scripts\Build-Help.ps1` to regenerate MAML only (no build, no parameter sync).
