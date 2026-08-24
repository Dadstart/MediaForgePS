# MediaForgePS Tests

This directory contains all test projects for MediaForgePS.

## Test Projects

| Project | Framework | Purpose |
|---------|-----------|---------|
| **MediaForgePS.Tests** | xUnit + Pester | Unit tests with mocks; fast, no external tools required |
| **MediaForgePS.ComponentTests** | xUnit | Cmdlet/service tests with real `ffmpeg`/`ffprobe` and small test media assets |
| **MediaForgePS.E2ETests** | xUnit | Pack → `Import-Module` Gallery layout smoke (real tools + sample media) |

## Running Tests

### All tests (dotnet + Pester)

`Build.ps1 -Test` runs `dotnet test` on the solution, then Pester (module configuration matches `-Configuration`):

```powershell
./scripts/Build.ps1 -Build -Test
```

Or run the layers separately:

```powershell
dotnet test
.\tests\MediaForgePS.Tests\Run-PesterTests.ps1
```

### C# unit tests (xUnit)

```powershell
dotnet test tests/MediaForgePS.Tests/MediaForgePS.Tests.csproj
```

CI collects line coverage from this project via `coverlet.msbuild` and fails when total line coverage drops below **70%**. Cobertura output is written to `tests/MediaForgePS.Tests/TestResults/coverage.cobertura.xml`.

```powershell
./scripts/Build.ps1 -Build -Test -Coverage
dotnet test tests/MediaForgePS.Tests/MediaForgePS.Tests.csproj /p:CollectCoverage=true
```

### Component tests

Requires `ffmpeg` and `ffprobe` on `PATH` and test assets under `MediaForgePS.ComponentTests/TestAssets`. Tests use `[SkippableFact]` / `SkipException` and skip when tools or assets are missing.

```powershell
dotnet test tests/MediaForgePS.ComponentTests/MediaForgePS.ComponentTests.csproj
```

Current coverage includes `Get-MediaFile`, `Get-AudioStreams`, convert cmdlets (`Convert-MediaFiles`, `Convert-MediaFileAdvanced`, `Convert-VideoFile`), `Export-MediaStream`, `Split-Chapters`, `Invoke-VideoCopy`, `Invoke-BonusFileProcessing`, the Media PSProvider, and `FfmpegService`/`ExecutableService` spaced-path integration. See [MediaForgePS.ComponentTests/TestAssets/README.md](MediaForgePS.ComponentTests/TestAssets/README.md) for asset details.

In CI, set `MEDIAFORGE_REQUIRE_COMPONENT_TESTS=1` so missing tools or assets fail the run instead of skipping.

### PowerShell unit tests (Pester)

Wired into `Build.ps1 -Test`. Recommended isolated runner:

```powershell
.\tests\MediaForgePS.Tests\Run-PesterTests.ps1
.\tests\MediaForgePS.Tests\Run-PesterTests.ps1 -ModuleConfiguration Release
```

Or manually:

```powershell
Invoke-Pester -Path tests/MediaForgePS.Tests/PowerShell -Configuration tests/MediaForgePS.Tests/PesterConfig.psd1
```

### E2E tests

Packs the built module via `scripts/Pack-Module.ps1`, imports the staged Gallery layout, and smokes `Get-MediaFile` plus factory cmdlets. Requires a complete build output (`MediaForgePS.dll`, manifest, `Formats/`) and `ffmpeg`/`ffprobe` for the probe path.

```powershell
dotnet test tests/MediaForgePS.E2ETests/MediaForgePS.E2ETests.csproj
```

Set `MEDIAFORGE_CONFIGURATION` to `Debug` or `Release` to select which build output to pack (Build.ps1 sets this automatically).
