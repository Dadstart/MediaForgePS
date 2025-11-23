# MediaForgePS Tests

This directory contains all test projects for MediaForgePS.

## Test Projects

- **MediaForgePS.Tests**: Unit tests using xUnit (C#) and Pester (PowerShell)
- **MediaForgePS.ComponentTests**: Component test infrastructure and placeholders
- **MediaForgePS.E2ETests**: End-to-end test infrastructure and placeholders

## Running Tests

### C# Unit Tests (xUnit)
```powershell
dotnet test tests/MediaForgePS.Tests/MediaForgePS.Tests.csproj
```

### PowerShell Unit Tests (Pester)

Using the provided script (recommended):
```powershell
.\tests\MediaForgePS.Tests\Run-PesterTests.ps1
```

Or manually:
```powershell
Invoke-Pester -Path tests/MediaForgePS.Tests/PowerShell -Configuration tests/MediaForgePS.Tests/PesterConfig.psd1
```

The script automatically removes the MediaForgePS module after tests complete.

### All Tests
```powershell
dotnet test
```
