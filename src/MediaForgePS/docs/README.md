# MediaForgePS help source (platyPS)

This folder holds the **Markdown source** for module help. PowerShell does not use these files directly.

- **Generate Markdown (one-time or after new cmdlets):**  
  From repo root, with the built module loaded:
  ```powershell
  Install-Module platyPS -Scope CurrentUser
  .\scripts\Update-Help.ps1
  ```
- **Edit** the `.md` files (description, parameters, examples).
- **Generate MAML:**  
  ```powershell
  .\scripts\Build-Help.ps1
  ```
- See **docs\platyPS-help-walkthrough.md** at the repo root for the full walkthrough.
