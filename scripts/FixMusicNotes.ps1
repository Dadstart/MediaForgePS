[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$Path
)

Get-ChildItem -LiteralPath $Path -Filter *.srt -File -Recurse | ForEach-Object {

    $file = $_

    # Skip empty files
    if ($file.Length -eq 0) {
        Write-Warning "Skipping empty file: $($file.FullName)"
        return
    }

    try {
        $content = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction Stop
    }
    catch {
        Write-Warning "Failed to read: $($file.FullName)"
        Write-Warning $_.Exception.Message
        return
    }

    if ([string]::IsNullOrEmpty($content)) {
        Write-Warning "No content read from: $($file.FullName)"
        return
    }

    $count10 = ([regex]::Matches($content, '\[\$10\]')).Count
    $count20 = ([regex]::Matches($content, '\[\$20\]')).Count
    $totalCount = $count10 + $count20

    if ($totalCount -eq 0) {
        return
    }

    $newContent = $content `
        -replace '\[\$10\]', '[♪♪♪]' `
        -replace '\[\$20\]', '[♪♪♪]'

    if ($PSCmdlet.ShouldProcess($file.FullName, "Replace $totalCount subtitle markers")) {
        Set-Content -LiteralPath $file.FullName -Value $newContent -Encoding UTF8
        Write-Host "Updated: $($file.FullName) ($totalCount replacements)"
    }
    else {
        Write-Host "Would update: $($file.FullName) ($totalCount replacements)"
    }
}