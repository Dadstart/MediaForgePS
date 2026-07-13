# MediaForgePS

PowerShell module for managing video files (MP4, MKV, etc.) directly from the terminal or other scripts.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.300 or later; see `global.json`)
- [PowerShell 7.6+](https://github.com/PowerShell/PowerShell/releases)
- [FFmpeg](https://ffmpeg.org/) (`ffmpeg` and `ffprobe` on `PATH`) — required for most cmdlets

Optional tools used by specific workflows:

| Tool | Used by |
|------|---------|
| [mkvtoolnix](https://mkvtoolnix.download/) (`mkvextract`) | Matroska VobSub extraction in `Convert-VideoFile`, `Export-Subtitles`, and related workflows |
| [Subtitle Edit](https://www.nikse.dk/subtitleedit/) | OCR subtitle conversion (`Convert-ImageSubtitlesToSrt`, `Export-Subtitles`, `Invoke-SubtitleOcrRepair`) |
| [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) | Image subtitle OCR (used with Subtitle Edit) |

## Building

```powershell
dotnet build
```

## Using the module

### Development session

Build the module, then launch an interactive session with the module imported:

```powershell
.\scripts\Launch.ps1
```

`Launch.ps1` builds if needed, opens a new PowerShell 7.6 window, imports the Debug build, and prints the process ID for attaching a debugger. Use `-Configuration Release` for a Release build.

### Import from a build

After `dotnet build`, import the module from the build output:

```powershell
Import-Module .\src\MediaForgePS\bin\Debug\net10.0\MediaForgePS.psd1 -Force
```

### Pack a Gallery-style module folder

```powershell
.\scripts\Build.ps1 -Configuration Release -Build
.\scripts\Pack-Module.ps1 -Configuration Release
```

This stages `artifacts/MediaForgePS` and `artifacts/MediaForgePS.<version>.zip` (not a C# NuGet package). CI uploads that zip as the pack artifact.

### Get help

Cmdlet help is shipped as MAML (`en-US\MediaForgePS.dll-Help.xml`). After importing:

```powershell
Get-Help Get-MediaFile -Full
Get-Help Convert-VideoFile -Examples
```

Markdown help source lives in `src\MediaForgePS\docs`. Regenerate MAML with `.\scripts\Update-Help.ps1` after changing cmdlets or help content.

## Testing

### Run all tests

```powershell
dotnet test
```

### Run C# unit tests only

```powershell
dotnet test tests/MediaForgePS.Tests/MediaForgePS.Tests.csproj
```

### Run component tests

Component tests exercise cmdlets with real `ffmpeg`/`ffprobe` (no mocks). They are skipped automatically when media tools or test assets are missing.

```powershell
dotnet test tests/MediaForgePS.ComponentTests/MediaForgePS.ComponentTests.csproj
```

See [tests/MediaForgePS.ComponentTests/TestAssets/README.md](tests/MediaForgePS.ComponentTests/TestAssets/README.md) for test media assets.

### Run PowerShell unit tests (Pester)

**Note:** The module is built automatically when needed.

Recommended — use the provided script:

```powershell
.\tests\MediaForgePS.Tests\Run-PesterTests.ps1
```

Or run manually from the repository root:

```powershell
Invoke-Pester -Path tests/MediaForgePS.Tests/PowerShell -OutputFile TestResults/PesterResults.xml -OutputFormat NUnitXml -Verbosity Detailed
```

To build manually first:

```powershell
dotnet build src/MediaForgePS/MediaForgePS.csproj -c Debug
```

See [tests/README.md](tests/README.md) for more detail on each test project.

## Code Quality

Before committing, ensure:

- `dotnet build` passes without errors
- `dotnet format --verify-no-changes` passes
- All tests pass

## Project Structure

```
MediaForgePS/
├── src/
│   └── MediaForgePS/          # Main module project
│       ├── Cmdlets/           # C# cmdlet implementations
│       ├── Models/            # Media and encoding types
│       ├── Services/          # FFmpeg, conversion, and workflow services
│       ├── docs/              # platyPS Markdown help source
│       └── MediaForgePS.psm1  # Module root script
├── tests/
│   ├── MediaForgePS.Tests/           # Unit tests (xUnit + Pester)
│   ├── MediaForgePS.ComponentTests/  # Cmdlet tests with real ffmpeg/ffprobe
│   └── MediaForgePS.E2ETests/        # End-to-end test infrastructure
├── scripts/                   # Build, help, and dev-session scripts
└── .github/workflows/         # CI/CD workflows
```

## Cmdlets

MediaForgePS exposes 18 cmdlets for media inspection, conversion, subtitles, chapters, and TV-library workflows. Full `Get-Help` text is built from Markdown under [src/MediaForgePS/docs](src/MediaForgePS/docs). Regenerate with `.\scripts\Update-Help.ps1` after changing cmdlets or help content.

### Choosing a conversion cmdlet

| Goal | Cmdlet |
|------|--------|
| Convert a folder of videos to MP4 with English audio mapping and optional caption extraction | `Convert-VideoFile` |
| Convert a list of files with encoder presets (`x264`, `x265`, `nvenc`) and optional custom mappings | `Convert-MediaFiles` |
| Convert one file with full control over video and audio settings | `Convert-MediaFileAdvanced` |

`Convert-VideoFile` is the opinionated, directory-oriented workflow (default encoder: **nvenc**). `Convert-MediaFiles` is the flexible batch cmdlet (default encoder: **x265** when `-DefaultVideoEncoder` is omitted). Both output `.mp4` files.

### Media inspection

| Cmdlet | Description |
|--------|-------------|
| `Get-MediaFile` | Media file metadata (format, streams, chapters) via ffprobe |
| `Get-AudioStreams` | Suggested English audio track mappings for conversion |
| `Export-MediaStream` | Extract one stream (video, audio, subtitle) without re-encoding |

```powershell
# Inspect a file before converting
$media = Get-MediaFile -Path ".\movie.mkv"
$media.Streams | Where-Object Type -eq 'audio' | Format-Table Index, Codec, Language

# Extract the first audio stream without re-encoding
Export-MediaStream -InputPath ".\movie.mkv" -OutputPath ".\audio.m4a" -Type Audio -Index 0
```

### Conversion

| Cmdlet | Description |
|--------|-------------|
| `Convert-VideoFile` | Batch video-to-MP4 conversion with auto audio mapping and optional caption extraction |
| `Convert-MediaFiles` | Batch conversion with configurable encoder and audio mappings |
| `Convert-MediaFileAdvanced` | Single-file conversion with explicit encoding settings |
| `New-VideoEncodingSettings` | Build `VideoEncodingSettings` for conversion cmdlets |
| `New-AudioTrackMapping` | Build copy or encode audio mappings |

```powershell
# Convert everything in a folder (default: NVENC HEVC)
Convert-VideoFile -InputPath "C:\Videos\Season1" -Recurse

# Batch convert with x265 and auto-detected English audio
Convert-MediaFiles -InputPath "C:\Source\*.mkv" -OutputDirectory "C:\Out" -DefaultVideoEncoder x265

# Full control over a single file
$settings = New-VideoEncodingSettings -Codec libx265 -CRF 20 -Preset slow
$mappings = Get-AudioStreams -InputPath "C:\movie.mkv"
Convert-MediaFileAdvanced -InputPath "C:\movie.mkv" -OutputPath "C:\Out\movie.mp4" `
    -VideoEncodingSettings $settings -AudioTrackMappings $mappings
```

`Get-AudioStreams` returns mappings for **English** audio only: DTS streams are copied; other codecs are encoded to AAC with channel-based bitrates.

### Subtitles

| Cmdlet | Description |
|--------|-------------|
| `Export-Subtitles` | Extract English subtitles; `-Ocr Auto\|Skip\|Force` controls image subtitle OCR |
| `Convert-ImageSubtitlesToSrt` | SUP/SUB → SRT via Subtitle Edit and Tesseract (alias: `Convert-SupToSrt`) |
| `Repair-Subtitles` | Fix common OCR errors in SRT files |
| `Invoke-SubtitleOcrRepair` | OCR image subtitles then repair the converted SRT files |

Several cmdlets accept `-Ocr` with values **Auto** (default), **Skip**, or **Force**:

- **Auto** — OCR image subtitles only when the source has a single exported subtitle format and it is not SRT.
- **Force** — OCR all exported image subtitle files.
- **Skip** — extract only; no OCR or repair.

```powershell
# Extract English subtitles and OCR image-based tracks when needed
Export-Subtitles -InputPath "C:\Videos\movie.mkv" -Ocr Auto

# Convert existing SUP files on disk, then repair OCR output
Invoke-SubtitleOcrRepair -InputPath "C:\Subs" -Recurse

# Fix OCR mistakes in SRT files in place
Repair-Subtitles -InputPath "C:\Subs\*.srt"
```

`Export-Subtitles` has alias `Export-RepairedSubtitles`. Folder input processes `*.mkv` files only.

### Chapters and TV workflows

| Cmdlet | Description |
|--------|-------------|
| `Split-Chapters` | Split a file by chapter ranges |
| `Split-SeriesChapters` | Split by chapters with TVDb episode naming |
| `Invoke-SeasonScan` | Fetch TVDb episode metadata for a season |
| `Invoke-VideoCopy` | Copy episodes into a folder using TVDb naming |
| `Invoke-SeriesProcessing` | Full season workflow (folders, scan, copy, chapters, captions) |
| `Invoke-BonusFileProcessing` | Convert and organize Plex bonus content |

```powershell
# Full season workflow: TVDb scan, copy, chapters, captions
Invoke-SeriesProcessing `
    -Title "My Show" -Season 1 `
    -InputPath "C:\Source" -FilePatterns "*.mkv" `
    -OutputPath "P:\TV" `
    -TvDbSeriesUrl "https://thetvdb.com/series/12345" `
    -ExtractChapters -Ocr Auto

# Split a combined season file into named episodes
$ranges = 1..10 | ForEach-Object { @{ Start = $_; End = $_ } }
Split-SeriesChapters -Title "My Show" -Season 1 -InputFile "C:\season1.mkv" `
    -ChapterRanges $ranges -TvDbSeriesUrl "https://thetvdb.com/series/12345"

# Process Plex bonus content (trailers, featurettes, etc.)
Invoke-BonusFileProcessing -InputPath "C:\Extras\Movie" -OutputPath "P:\Movies\Movie"
```

`Invoke-SeriesProcessing` runs these steps in order: create folder structure, scan TVDb, copy episodes, optionally extract chapters, optionally extract captions with OCR. It fails if TVDb returns no episodes or no files are copied.

See [src/MediaForgePS/docs](src/MediaForgePS/docs) for per-cmdlet help source and [docs/platyPS-help-walkthrough.md](docs/platyPS-help-walkthrough.md) for the help authoring workflow.
