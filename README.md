# MediaForgePS

PowerShell module for managing video files (MP4, MKV, etc.) directly from the terminal or other scripts.

## Requirements

- .NET 10 SDK
- PowerShell 7.5

## Building

```powershell
dotnet build
```

## Testing

### Run all tests
```powershell
dotnet test
```

### Run C# unit tests only
```powershell
dotnet test tests/MediaForgePS.Tests/MediaForgePS.Tests.csproj
```

### Run PowerShell unit tests (Pester)

**Note:** The module will be automatically built if needed when running Pester tests.

```powershell
# From repository root
$configPath = "tests/MediaForgePS.Tests/PesterConfig.psd1"
$config = Import-PowerShellDataFile -Path $configPath
$pesterConfig = New-PesterConfiguration -Hashtable $config
$pesterConfig.Run.Path = "tests/MediaForgePS.Tests/PowerShell"
Invoke-Pester -Configuration $pesterConfig
```

Or run with inline parameters (simpler):
```powershell
# From repository root
Invoke-Pester -Path tests/MediaForgePS.Tests/PowerShell -OutputFile TestResults/PesterResults.xml -OutputFormat NUnitXml -Verbosity Detailed
```

**Note:** If you prefer to build manually first:
```powershell
dotnet build src/MediaForgePS/MediaForgePS.csproj -c Debug
```

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
│       └── MediaForgePS.psm1  # Module root script
├── tests/
│   ├── MediaForgePS.Tests/           # Unit tests (xUnit + Pester)
│   ├── MediaForgePS.ComponentTests/  # Component test infrastructure
│   └── MediaForgePS.E2ETests/        # E2E test infrastructure
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
| `Convert-VideoFile` | Batch MKV → MP4 with auto audio mapping and optional captions |
| `Convert-MediaFiles` | Batch conversion with configurable encoder and audio mappings |
| `Convert-MediaFileAdvanced` | Single-file conversion with explicit encoding settings |
| `New-VideoEncodingSettings` | Build `VideoEncodingSettings` for conversion cmdlets |
| `New-AudioTrackMapping` | Build copy or encode audio mappings |

### Subtitles

| Cmdlet | Description |
|--------|-------------|
| `Export-Subtitles` | Extract English subtitles; optional OCR and SRT repair |
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
