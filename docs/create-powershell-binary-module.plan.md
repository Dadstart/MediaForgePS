<!-- a0407b6e-be9d-4faa-87f6-6231326c408f b9819c96-5270-4434-81dd-a45c232d3edc -->
# Create PowerShell Binary Module with Get-Holiday Cmdlet

## Project Structure

Create the following directory structure following standard PowerShell binary module layout:

```
MediaForgePS/
├── .github/
│   └── workflows/
│       └── ci.yml
├── src/
│   └── Dadstart.Labs.MediaForgePS/
│       ├── Dadstart.Labs.MediaForgePS.csproj
│       ├── Cmdlets/
│       │   └── GetHolidayCommand.cs
│       ├── Models/
│       │   └── Holiday.cs
│       └── Services/
│           └── HolidayScraper.cs
├── tests/
│   ├── Dadstart.Labs.MediaForgePS.Tests/
│   │   ├── Dadstart.Labs.MediaForgePS.Tests.csproj
│   │   ├── Cmdlets/
│   │   │   └── GetHolidayCommandTests.cs
│   │   └── Services/
│   │       └── HolidayScraperTests.cs
│   └── MediaForgePS.Tests.ps1
├── MediaForgePS/
│   ├── bin/
│   │   └── (DLL will be copied here during build)
│   ├── Dadstart.Labs.MediaForgePS.psd1
│   └── Dadstart.Labs.MediaForgePS.psm1
├── Directory.Build.props
├── .editorconfig
├── .gitignore
└── MediaForgePS.sln
```

## Implementation Details

### 1. Solution and Project Files

- **MediaForgePS.sln**: Solution file with main library and test projects
- **Directory.Build.props**: Shared MSBuild properties for .NET 9, C# 13, LangVersion, and common package references
- **.editorconfig**: Editor configuration following C# 13 best practices
- **.gitignore**: Standard .NET gitignore patterns

### 2. Main Library Project (src/Dadstart.Labs.MediaForgePS/)

**Dadstart.Labs.MediaForgePS.csproj**:

- Target framework: `net9.0`
- C# language version: `13.0`
- NuGet packages:
  - `PowerShellStandard.Library` (latest compatible with .NET 9)
  - `HtmlAgilityPack` (latest version)
  - `System.Net.Http` (if needed)

**Models/Holiday.cs**:

- Record type with properties for all available holiday details from timeanddate.com
- Properties: Date, Name, Type, Description, Observances, and any other fields available

**Services/HolidayScraper.cs**:

- Service class for scraping timeanddate.com
- Uses HttpClient (injected or static) and HtmlAgilityPack
- Method: `GetHolidaysAsync(DateTime date)` returns `Task<List<Holiday>>`
- Parses HTML table structure from timeanddate.com US holidays page
- Extracts all available holiday details

**Cmdlets/GetHolidayCommand.cs**:

- Inherits from `PSCmdlet`
- Two parameter sets:
  - `SingleDate`: `-Date` parameter (mandatory, position 0)
  - `DateRange`: `-StartDate` and `-EndDate` parameters (both mandatory, positions 0 and 1)
- Uses `ProcessRecord()` to handle cmdlet execution
- Calls `HolidayScraper` service to fetch holidays
- Outputs `Holiday` objects via `WriteObject()`
- Proper async/await handling

### 3. Module Manifest (MediaForgePS/Dadstart.Labs.MediaForgePS.psd1)

- Module version: `1.0.0`
- PowerShell version: `7.5`
- RootModule: `Dadstart.Labs.MediaForgePS.dll` (in bin folder)
- CmdletsToExport: `@('Get-Holiday')`
- Proper GUID generation
- Module description

### 4. Module Script (MediaForgePS/Dadstart.Labs.MediaForgePS.psm1)

- Empty or minimal script module file (binary module handles functionality)

### 5. C# Unit Tests (tests/Dadstart.Labs.MediaForgePS.Tests/)

**Dadstart.Labs.MediaForgePS.Tests.csproj**:

- Target framework: `net9.0`
- Test framework: `xunit`
- Mocking: `Moq`
- Reference to main library project

**Cmdlets/GetHolidayCommandTests.cs**:

- Tests for both parameter sets
- Mock `HolidayScraper` service
- Verify correct date range generation
- Verify output objects

**Services/HolidayScraperTests.cs**:

- Mock HttpClient responses
- Test HTML parsing logic
- Verify all holiday fields are extracted
- Test error handling

### 6. PowerShell Unit Tests (tests/MediaForgePS.Tests.ps1)

- Pester 5.x test file
- Tests for `Get-Holiday -Date`
- Tests for `Get-Holiday -StartDate -EndDate`
- Verify output structure
- Mock web requests to avoid external dependencies in tests

### 7. CI/CD Workflow (.github/workflows/ci.yml)

- Build: `dotnet build`
- Test: `dotnet test`
- Format check: `dotnet format --verify-no-changes`
- Pack artifacts (no publish)

### 8. Build Configuration

- Post-build step to copy DLL and dependencies to `MediaForgePS/bin/` folder
- Ensure all required DLLs are included (HtmlAgilityPack, PowerShellStandard.Library dependencies)

## Key Implementation Notes

- Use proper async/await patterns in cmdlet (avoid `.GetAwaiter().GetResult()` where possible)
- Implement proper error handling for network failures and parsing errors
- Follow C# 13 coding standards from workspace rules
- Use records for Holiday model (immutable data)
- Use dependency injection pattern for HttpClient in scraper service
- Ensure cross-platform compatibility (.NET 9 is cross-platform)

### To-dos

- [ ] Create solution file, Directory.Build.props, .editorconfig, and .gitignore
- [ ] Create main C# library project with proper .NET 9 and C# 13 configuration, add NuGet packages (PowerShellStandard.Library, HtmlAgilityPack)
- [ ] Create Holiday record type with all available properties from timeanddate.com
- [ ] Implement HolidayScraper service class with HttpClient and HtmlAgilityPack to scrape timeanddate.com US holidays page
- [ ] Implement GetHolidayCommand cmdlet with two parameter sets (SingleDate and DateRange)
- [ ] Create PowerShell module manifest (.psd1) and script module (.psm1) files in MediaForgePS folder
- [ ] Create C# test project with xUnit and Moq, add reference to main project
- [ ] Write C# unit tests for GetHolidayCommand and HolidayScraper with mocked dependencies
- [ ] Write Pester tests for Get-Holiday cmdlet with mocked web requests
- [ ] Configure build to copy DLL and dependencies to MediaForgePS/bin/ folder
- [ ] Create GitHub Actions workflow for build, test, lint, and pack