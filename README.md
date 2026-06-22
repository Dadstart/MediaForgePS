# MediaForgePS

PowerShell module for managing video files (MP4, MKV, etc.) directly from the terminal or other scripts.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.300 or later; see `global.json`)
- [PowerShell 7.5+](https://github.com/PowerShell/PowerShell/releases)
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

## Using the module (development)

Build the module, then launch an interactive session with the module imported:

```powershell
.\scripts\Launch.ps1
```

`Launch.ps1` builds if needed, opens a new PowerShell 7.5 window, imports the Debug build, and prints the process ID for attaching a debugger. Use `-Configuration Release` for a Release build.

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

Full `Get-Help` text is built from Markdown under `src/MediaForgePS/docs`. Regenerate with `.\scripts\Update-Help.ps1` after changing cmdlets or help content.

### Media inspection

| Cmdlet | Description |
|--------|-------------|
| `Get-MediaFile` | Media file metadata (format, streams, chapters) via ffprobe |
| `Get-AudioStreams` | Suggested audio track mappings for conversion |
| `Export-MediaStream` | Extract one stream (video, audio, subtitle) without re-encoding |

### Conversion

| Cmdlet | Description |
|--------|-------------|
| `Convert-VideoFile` | Batch video-to-MP4 conversion with auto audio mapping and optional caption extraction (use `-Ocr` for image subtitle OCR) |
| `Convert-MediaFiles` | Batch conversion with configurable encoder and audio mappings |
| `Convert-MediaFileAdvanced` | Single-file conversion with explicit encoding settings |
| `New-VideoEncodingSettings` | Build `VideoEncodingSettings` for conversion cmdlets |
| `New-AudioTrackMapping` | Build copy or encode audio mappings |

### Subtitles

| Cmdlet | Description |
|--------|-------------|
| `Export-Subtitles` | Extract English subtitles; use `-Ocr` for image subtitle OCR and SRT repair |
| `Convert-ImageSubtitlesToSrt` | SUP/SUB → SRT via Subtitle Edit and Tesseract |
| `Repair-Subtitles` | Fix common OCR errors in SRT files |
| `Invoke-SubtitleOcrRepair` | OCR image subtitles then repair SRT files on disk |

### Chapters and TV workflows

| Cmdlet | Description |
|--------|-------------|
| `Split-Chapters` | Split a file by chapter ranges |
| `Split-SeriesChapters` | Split by chapters with TVDb episode naming |
| `Invoke-SeasonScan` | Fetch TVDb episode metadata for a season |
| `Invoke-VideoCopy` | Copy episodes into a folder using TVDb naming |
| `Invoke-SeriesProcessing` | Full season workflow (folders, scan, copy, chapters, captions) |
| `Invoke-BonusFileProcessing` | Convert and organize Plex bonus content |

See [src/MediaForgePS/docs](src/MediaForgePS/docs) for per-cmdlet help source and [docs/platyPS-help-walkthrough.md](docs/platyPS-help-walkthrough.md) for the help authoring workflow.
