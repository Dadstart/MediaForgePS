# MediaForgePS

PowerShell binary module for retrieving US holidays from timeanddate.com.

## Requirements

- PowerShell 7.5 or later
- .NET 9.0 runtime

## Installation

### From Source

1. Clone the repository:
```powershell
git clone https://github.com/yourusername/MediaForgePS.git
cd MediaForgePS
```

2. Build the module:
```powershell
dotnet build
```

3. Import the module:
```powershell
Import-Module (Resolve-Path .\MediaForgePS\Dadstart.Labs.MediaForgePS.psd1).Path
```

Alternatively, you can import using the module directory:
```powershell
Import-Module .\MediaForgePS
```

### Module Path Installation

To install the module in your PowerShell module path:

```powershell
# Copy the MediaForgePS folder to your modules directory
$modulePath = Join-Path $env:PSModulePath.Split(';')[0] 'Dadstart.Labs.MediaForgePS'
Copy-Item -Path .\MediaForgePS -Destination $modulePath -Recurse -Force

# Import the module
Import-Module Dadstart.Labs.MediaForgePS
```

## Usage

### Get Holidays for a Single Date

```powershell
# Get holidays for a specific date
Get-Holiday -Date (Get-Date '2024-01-01')

# Get holidays for today
Get-Holiday -Date (Get-Date)
```

### Get Holidays for a Date Range

```powershell
# Get holidays for a date range
Get-Holiday -StartDate (Get-Date '2024-01-01') -EndDate (Get-Date '2024-12-31')

# Get holidays for the current month
$startDate = (Get-Date).Date.AddDays(-(Get-Date).Day + 1)
$endDate = $startDate.AddMonths(1).AddDays(-1)
Get-Holiday -StartDate $startDate -EndDate $endDate
```

### Output Properties

The `Get-Holiday` cmdlet returns `Holiday` objects with the following properties:

- **Date**: DateTime - The date of the holiday
- **Name**: string - The name of the holiday
- **Type**: string - The type of holiday (e.g., "Federal Holiday", "State Holiday")
- **Description**: string - Description of the holiday
- **Observances**: string - Additional observances information

### Examples

```powershell
# Get all holidays in 2024
$holidays = Get-Holiday -StartDate '2024-01-01' -EndDate '2024-12-31'

# Filter for federal holidays
$federalHolidays = $holidays | Where-Object { $_.Type -like '*Federal*' }

# Display holidays in a formatted table
Get-Holiday -StartDate '2024-01-01' -EndDate '2024-01-31' | 
    Format-Table Date, Name, Type -AutoSize

# Export holidays to CSV
Get-Holiday -StartDate '2024-01-01' -EndDate '2024-12-31' | 
    Export-Csv -Path 'holidays.csv' -NoTypeInformation
```

## Building from Source

### Prerequisites

- .NET 9.0 SDK
- PowerShell 7.5 or later

### Build Steps

1. Restore dependencies:
```powershell
dotnet restore
```

2. Build the solution:
```powershell
dotnet build
```

3. Run tests:
```powershell
dotnet test
```

4. Format check:
```powershell
dotnet format --verify-no-changes
```

5. Pack the module:
```powershell
dotnet pack --configuration Release
```

The build process automatically copies the compiled DLL and dependencies to `MediaForgePS/bin/` directory.

## Project Structure

```
MediaForgePS/
├── .github/
│   └── workflows/
│       └── ci.yml              # GitHub Actions CI workflow
├── src/
│   └── Dadstart.Labs.MediaForgePS/
│       ├── Cmdlets/
│       │   └── GetHolidayCommand.cs
│       ├── Models/
│       │   └── Holiday.cs
│       └── Services/
│           └── HolidayScraper.cs
├── tests/
│   ├── Dadstart.Labs.MediaForgePS.Tests/  # C# unit tests
│   └── MediaForgePS.Tests.ps1             # Pester tests
├── MediaForgePS/
│   ├── bin/                                # Compiled DLLs (generated)
│   ├── Dadstart.Labs.MediaForgePS.psd1    # Module manifest
│   └── Dadstart.Labs.MediaForgePS.psm1    # Module script
└── MediaForgePS.sln                        # Solution file
```

## Development

### Running Tests

#### C# Unit Tests

```powershell
dotnet test
```

#### PowerShell Tests (Pester)

```powershell
# Install Pester if not already installed
Install-Module -Name Pester -Force -SkipPublisherCheck

# Run Pester tests
Invoke-Pester tests/MediaForgePS.Tests.ps1
```

### Code Style

The project follows C# 13 coding standards. Run the formatter to ensure code style compliance:

```powershell
dotnet format
```

## CI/CD

The project includes a GitHub Actions workflow (`.github/workflows/ci.yml`) that:

- Builds the solution
- Runs unit tests
- Verifies code formatting
- Creates NuGet packages

## License

See LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Ensure all tests pass
5. Submit a pull request

## Notes

- The module scrapes holiday data from timeanddate.com
- Network connectivity is required to retrieve holiday information
- The module handles errors gracefully and provides error messages via `WriteError`
