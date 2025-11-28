 # Agent Instructions

## General

- Use modern best patterns and practices
- Prefer experimenting with cutting edge technologies
- Prefer open source projects
- All changes should be reviewed before commit
- Use PowerShell 7.5 in the editor and terminal

## Local Workflow

- Run `dotnet build` at the repo root before committing to surface analyzer and lint diagnostics early.
- Run `dotnet format --verify-no-changes` to ensure analyzer fixes are applied and the codebase stays lint clean.

## Code Style

### dotnet

- Use dotnet 9

### C#

#### General
- Use C# 13
- Root C# namespace is Dadstart.Labs.MediaForge
- Do not add copyright file headers to C# files
- Do not use StyleCop
- Do not use `this.` when unneeded
- Prefer using statements over fully qualified type names
- Omit braces for single-line C# statement bodies
- Do not surround single-line statement blocks with curly braces (e.g., `if (condition) statement;` not `if (condition) { statement; }`)
- Put return statements on separate lines
- Prefer records when the type is primarily data and immutability and value-equality semantics are desirable
- Use classes/structs when mutable behavior, identity semantics, or complex lifecycle/behavior is primary.
- Do not put return on same line as other statements
- Do not put try and catch blocks on the same line

#### Async operation
- Using `ConfigureAwait(false)` on all async calls
- Use `GetAwaiter().GetResult()` to synchronously wait for the async operation in PowerShell code

#### Naming Conventions
- Public members: PascalCase
- Constants: PascalCase
- Private instance fields: Prefix with underscore then camelCase
- Private static fields: Prefix with underscore then camelCase
- Enums: Use PascalCase for members

#### Documentation Comments
- Property comments should not use "Gets a" or "Gets the". Use direct descriptions instead (e.g., "Dictionary of..." instead of "Gets a dictionary of...")
- Do not include `<exception>` tags in XML documentation comments


### PowerShell

- Use PowerShell 7.5

### Ffmpeg and Ffprobe

- Use proper casing for 'Ffprobe' and 'Ffmpeg' when naming code elements

### JSON

- Do not add comments to JSON files unless the agent knows that comments are allowed in that specific file format (e.g., JSONC, JSON5, or tool-specific JSON parsers that support comments)

## Code Quality

- Follow Clean Code principles
- Flag deep nesting, long functions, and magic numbers
- Suggest meaningful names
- Avoid unnecessary comments

## Terminal / Scripting

- Use PowerShell 7.5 for cross-platform scripting

## Architecture

- Modern cross-platform dotnet library that implements:
  - 'MediaForge' PowerShell module with cmdlets
  - PSProvider for browsing the internal streams of media files
  - Class types with PowerShell custom formatting

## CI/CD

- Use GitHub actions for CI/CD to do the following
  - build, test, lint
  - pack artifacts
  - No CD. Do not publish.

## Testing

### General
- Full testing required for all code
- Use modern testing methodologies
- Always add unit tests for code changes
- Prompt to add component testing
- Prompt to add functional testing

### C#
- Use xUnit for C# testing
- Use Moq for C# mocks


### PowerShell
- Use Pester for PowerShell testing
