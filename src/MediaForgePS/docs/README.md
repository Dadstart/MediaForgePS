# MediaForgePS help source (platyPS)

This folder holds the **Markdown source** for module help. PowerShell does not use these files directly.

- **Generate Markdown (one-time or after new cmdlets):**  
  From repo root, with the built module loaded:
  ```powershell
  Install-Module platyPS
  Import-Module ".\src\MediaForgePS\bin\Debug\net9.0" -Force
  New-MarkdownHelp -Module MediaForgePS -OutputFolder ".\src\MediaForgePS\docs" -Force
  ```
- **Edit** the `.md` files (description, parameters, examples).
- **Generate MAML:**  
  ```powershell
  New-ExternalHelp -Path ".\src\MediaForgePS\docs" -OutputPath ".\src\MediaForgePS\en-US"
  ```
- See **docs\platyPS-help-walkthrough.md** at the repo root for the full walkthrough.
