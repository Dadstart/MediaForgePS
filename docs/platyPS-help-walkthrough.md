# platyPS help walkthrough (Option 1A)

Follow these steps to add full `Get-Help` documentation for MediaForgePS cmdlets using platyPS.

---

## Step 1: Install platyPS

In PowerShell 7.5:

```powershell
Install-Module platyPS -Scope CurrentUser
```

Confirm the module is available:

```powershell
Get-Command New-MarkdownHelp, New-ExternalHelp
```

---

## Step 2: Build the project

platyPS needs the module to be loadable so it can read cmdlet and parameter metadata. Build from the repo root:

```powershell
cd M:\repos\MediaForgePS
dotnet build
```

The module will be in `src\MediaForgePS\bin\Debug\<target-framework>\` (for example `net9.0`), with `MediaForgePS.psd1`, `MediaForgePS.psm1`, and `MediaForgePS.dll` copied there.

---

## Fast path (recommended)

Use the project scripts to avoid manual path and framework drift:

```powershell
.\scripts\Update-Help.ps1
```

This updates Markdown and regenerates `src\MediaForgePS\en-US\MediaForgePS.dll-Help.xml`.

---

## Step 3: Create the docs folder and scaffold Markdown

Create a folder for help source (Markdown) next to the module source. From the repo root:

```powershell
$moduleDir = "M:\repos\MediaForgePS\src\MediaForgePS"
$outputDir = "$moduleDir\docs"
New-Item -ItemType Directory -Path $outputDir -Force
```

Load the **built** module (so it sees the real cmdlets), then generate one Markdown file per cmdlet:

```powershell
# Use the built module (bin/Debug or bin/Release)
$targetFramework = ([xml](Get-Content .\Shared.props -Raw)).Project.PropertyGroup.TargetFramework
$builtModule = "M:\repos\MediaForgePS\src\MediaForgePS\bin\Debug\$targetFramework"
Import-Module $builtModule -Force

New-MarkdownHelp -Module MediaForgePS -OutputFolder $outputDir -Force
```

This creates files like `Export-Subtitles.md`, `Get-MediaFile.md`, `Repair-Subtitles.md`, etc., in `src\MediaForgePS\docs\`.

---

## Step 4: Edit the Markdown files

Open each `.md` file under `src\MediaForgePS\docs\` and fill in:

- **Synopsis** – One-line description.
- **Description** – Full description (and optionally **Notes**).
- **Parameter descriptions** – Replace placeholder text with clear explanations.
- **Examples** – Add `\`\`powershell` blocks with example commands and, if you like, output.

Example for `Export-Subtitles.md`:

```markdown
---
schema: 2.0.0
---

# Export-Subtitles

## SYNOPSIS
Exports English subtitle streams from media files and optionally converts image subtitles to SRT via OCR.

## SYNTAX
...
## DESCRIPTION
Export-Subtitles extracts English subtitle tracks from MKV (or other) media files. When you use -Ocr, it also converts image-based formats (SUP, SUB) to SRT using Subtitle Edit and Tesseract, and can repair SRT text unless -SkipRepair is specified.
...
## EXAMPLES

### Example 1: Export subtitles from a single file
```powershell
Export-Subtitles -InputPath "C:\Videos\movie.mkv"
```

### Example 2: Export and convert image subtitles to SRT with repair
```powershell
Get-ChildItem "C:\Videos" -Filter *.mkv | Export-Subtitles -Ocr -BackupPath "C:\Backup\srts"
```
...
```

Save all edited files.

---

## Step 5: Generate the MAML help file (en-US)

From the repo root, generate the external help XML that PowerShell will load:

```powershell
$docsPath = "M:\repos\MediaForgePS\src\MediaForgePS\docs"
$enUsPath = "M:\repos\MediaForgePS\src\MediaForgePS\en-US"
New-Item -ItemType Directory -Path $enUsPath -Force
New-ExternalHelp -Path $docsPath -OutputPath $enUsPath
```

This creates `src\MediaForgePS\en-US\MediaForgePS.dll-Help.xml`. PowerShell expects a folder named `en-US` next to the module’s `.psd1`/`.psm1`, with the module’s help XML inside it.

---

## Step 6: Ship the help with the module

The `.csproj` is already set up to copy `en-US\*.xml` into the build output. After building, `bin\Debug\<target-framework>\en-US\MediaForgePS.dll-Help.xml` will be present next to the module files.

Rebuild so the help is in the output directory:

```powershell
dotnet build
```

Load the module from the build output and verify help:

```powershell
$targetFramework = ([xml](Get-Content .\Shared.props -Raw)).Project.PropertyGroup.TargetFramework
Import-Module "M:\repos\MediaForgePS\src\MediaForgePS\bin\Debug\$targetFramework" -Force
Get-Help Export-Subtitles -Full
Get-Help Export-Subtitles -Examples
```

---

## Step 7 (optional): Regenerate after adding or changing cmdlets

When you add new cmdlets or change parameters:

1. **New cmdlets** – Run `New-MarkdownHelp -Module MediaForgePS -OutputFolder $outputDir -Force` again; platyPS will add new `.md` files. Edit them and then run `New-ExternalHelp` as in Step 5.
2. **Parameter changes** – Update the corresponding `.md` (or re-run `New-MarkdownHelp -Force` to refresh parameter blocks, then re-edit descriptions and examples).
3. **Regenerate XML** – Run `New-ExternalHelp -Path $docsPath -OutputPath $enUsPath` and rebuild.

---

## Folder layout summary

```
src/MediaForgePS/
├── MediaForgePS.psd1
├── MediaForgePS.psm1
├── docs/                    ← Markdown source (you edit these)
│   ├── Export-Subtitles.md
│   ├── Get-MediaFile.md
│   └── ...
├── en-US/                   ← Generated; copied to build output
│   └── MediaForgePS.dll-Help.xml
└── bin/Debug/<target-framework>/
    ├── MediaForgePS.psd1
    ├── MediaForgePS.psm1
    ├── MediaForgePS.dll
    └── en-US/
        └── MediaForgePS.dll-Help.xml
```

---

## Troubleshooting

- **"Module MediaForgePS not found"** – Build first and use the full path to the built folder when calling `Import-Module` before `New-MarkdownHelp`.
- **Help not showing** – Ensure `en-US\MediaForgePS.dll-Help.xml` exists in the **same** directory as the loaded module (the folder containing `.psd1`). Rebuild after adding or updating `en-US`.
- **Stale help** – After editing `.md` files, run `New-ExternalHelp` again and rebuild.
