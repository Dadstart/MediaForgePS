# MediaForgePS Tests

This directory contains all test projects for MediaForgePS.

## Test Projects

| Project | Framework | Purpose |
|---------|-----------|---------|
| **MediaForgePS.Tests** | xUnit + Pester | Unit tests with mocks; fast, no external tools required |
| **MediaForgePS.ComponentTests** | xUnit | Cmdlet tests with real `ffmpeg`/`ffprobe` and small test media assets |
| **MediaForgePS.E2ETests** | xUnit | End-to-end test infrastructure (placeholder tests today) |

## Running Tests

### All tests

```powershell
dotnet test
```

### C# unit tests (xUnit)

```powershell
dotnet test tests/MediaForgePS.Tests/MediaForgePS.Tests.csproj
```

### Component tests

Requires `ffmpeg` and `ffprobe` on `PATH` and test assets under `MediaForgePS.ComponentTests/TestAssets`. Tests use `[SkippableFact]` and skip when tools or assets are missing.

```powershell
dotnet test tests/MediaForgePS.ComponentTests/MediaForgePS.ComponentTests.csproj
```

Current coverage includes `Get-MediaFile` and `Convert-MediaFiles`. See [MediaForgePS.ComponentTests/TestAssets/README.md](MediaForgePS.ComponentTests/TestAssets/README.md) for asset details.

### PowerShell unit tests (Pester)

Recommended — use the provided script (runs in an isolated process and unloads the module afterward):

```powershell
.\tests\MediaForgePS.Tests\Run-PesterTests.ps1
```

Or manually:

```powershell
Invoke-Pester -Path tests/MediaForgePS.Tests/PowerShell -Configuration tests/MediaForgePS.Tests/PesterConfig.psd1
```

### E2E tests

```powershell
dotnet test tests/MediaForgePS.E2ETests/MediaForgePS.E2ETests.csproj
```
