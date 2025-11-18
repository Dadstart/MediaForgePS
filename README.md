# MediaForgePS

PowerShell module for managing video files (MP4, MKV, etc.) directly from the terminal or other scripts.

## Requirements

- .NET 9 SDK
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
```powershell
Invoke-Pester -Path tests/MediaForgePS.Tests/PowerShell -Configuration tests/MediaForgePS.Tests/PesterConfig.psd1
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
│       ├── MediaForgePS.psd1  # Module manifest
│       └── MediaForgePS.psm1  # Module root script
├── tests/
│   ├── MediaForgePS.Tests/           # Unit tests (xUnit + Pester)
│   ├── MediaForgePS.ComponentTests/  # Component test infrastructure
│   └── MediaForgePS.E2ETests/        # E2E test infrastructure
└── .github/workflows/         # CI/CD workflows
```

## Current Cmdlets

- `Add-Number` - Adds two numbers
- `Subtract-Number` - Subtracts two numbers
