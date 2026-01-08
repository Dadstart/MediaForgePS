# Debugging MediaForgePS PowerShell Cmdlets

This guide explains how to debug PowerShell cmdlets in MediaForgePS, specifically `Convert-AutoMediaFiles` and other cmdlets.

## Overview

MediaForgePS provides several debugging approaches:

1. **VS Code Attach Debugger** - Attach VS Code debugger to a PowerShell process (recommended)
2. **DebuggerService Breakpoints** - Use built-in breakpoint functionality
3. **Component Tests** - Write unit/component tests for isolated debugging

## Method 1: VS Code Attach Debugger (Recommended)

This is the most convenient method for interactive debugging with breakpoints, variable inspection, and step-through debugging.

### Step 1: Build the Module

First, ensure the module is built in Debug configuration:

```powershell
.\scripts\Build.ps1 -Configuration Debug -Build -Publish
```

### Step 2: Launch PowerShell with Module Loaded

Launch a new PowerShell instance with the module imported:

```powershell
.\scripts\Launch.ps1 -Configuration Debug
```

This will:
- Start a new PowerShell window
- Import the MediaForgePS module
- Display the **Process ID (PID)** in the window

**Note the PID** - you'll need it for the next step.

### Step 3: Attach VS Code Debugger

1. In VS Code, open the **Run and Debug** view (Ctrl+Shift+D)
2. Select **"Attach to PowerShell (Debug)"** from the dropdown
3. Press F5 or click the green play button
4. VS Code will show a process picker - select the PowerShell process with the PID you noted
   - Look for `pwsh.exe` or `powershell.exe` processes
   - The window title should show "MediaForgePS Debug Session"

### Step 4: Set Breakpoints and Debug

1. Set breakpoints in your C# code (e.g., in `ConvertAutoMediaFilesCommand.cs`)
2. In the PowerShell window, run your cmdlet:
   ```powershell
   Convert-AutoMediaFiles -InputPath "C:\path\to\video.mp4" -OutputDirectory "C:\output"
   ```
3. Execution will pause at your breakpoints
4. Use VS Code's debug controls to step through code, inspect variables, etc.

### Troubleshooting

- **Can't find the process**: Make sure the PowerShell window from `Launch.ps1` is still open
- **Breakpoints not hitting**: Ensure you built in Debug configuration and symbols are available
- **Wrong process**: Check the window title or PID to ensure you're attaching to the correct PowerShell instance

## Method 2: DebuggerService Breakpoints

The `CmdletBase` class includes built-in breakpoint support through `DebuggerService`. This allows you to break at PowerShell cmdlet lifecycle stages.

### Enable Breakpoints

In your PowerShell session (after importing the module), configure breakpoints:

```powershell
# Get the DebuggerService instance
$debugger = [Dadstart.Labs.MediaForge.Services.System.DebuggerService]::new()

# Enable breaking at BeginProcessing (when cmdlet starts)
$debugger.PowerShellBreakOnBeginProcessing = $true

# Enable breaking at ProcessRecord (when processing each pipeline item)
$debugger.PowerShellBreakOnProcessRecord = $true

# Enable breaking at EndProcessing (when cmdlet finishes)
$debugger.PowerShellBreakOnEndProcessing = $true

# Or break at all stages
[Dadstart.Labs.MediaForge.Services.System.DebuggerService]::BreakAll = $true
```

### Force Debugging Mode

If you want breakpoints to trigger even without a debugger attached:

```powershell
[Dadstart.Labs.MediaForge.Services.System.DebuggerService]::ForceDebugging = $true
```

**Note**: This will cause `Debugger.Break()` to be called, which will prompt you to attach a debugger if one isn't already attached.

### Using with VS Code

1. Launch PowerShell with `.\scripts\Launch.ps1`
2. Attach VS Code debugger (Method 1)
3. Configure DebuggerService breakpoints in PowerShell
4. Run your cmdlet - execution will break at the configured stages

## Method 3: Component Tests

For isolated debugging of specific functionality, write component tests:

### Example Component Test

```csharp
// In tests/MediaForgePS.ComponentTests/Cmdlets/ConvertAutoMediaFilesCommandComponentTests.cs
[Fact]
public void ConvertAutoMediaFilesCommand_ProcessesFile_Successfully()
{
    // Arrange
    var cmdlet = new ConvertAutoMediaFilesCommand();
    cmdlet.InputPath = new[] { "test-video.mp4" };
    cmdlet.OutputDirectory = "output";
    
    // Act & Debug
    // Set breakpoints here and run the test with debugger attached
    cmdlet.Begin();
    cmdlet.Process();
    cmdlet.End();
    
    // Assert
    // ...
}
```

Run the test with debugger attached:

```powershell
dotnet test --filter "FullyQualifiedName~ConvertAutoMediaFilesCommandComponentTests" --logger "console;verbosity=detailed"
```

## Debugging Tips

### 1. Check Logging Output

The cmdlet uses structured logging. Check the PowerShell console for log output:

```powershell
# Enable verbose output to see debug logs
$VerbosePreference = 'Continue'
Convert-AutoMediaFiles -InputPath "video.mp4" -OutputDirectory "output" -Verbose
```

### 2. Inspect Service Dependencies

The cmdlet uses dependency injection. You can inspect services:

```powershell
# After importing module, services are initialized
# Check logs to see service initialization
```

### 3. Debug Async Operations

The cmdlet uses `GetAwaiter().GetResult()` for async operations. When debugging:
- Set breakpoints in async methods
- Use VS Code's async debugging features
- Check the call stack to understand async flow

### 4. Debug FFmpeg Integration

If debugging FFmpeg-related issues:
- Check `FfmpegService` and `FfprobeService` logs
- Inspect `FfmpegProgress` objects during conversion
- Review `FfmpegConversionException` details when conversions fail

## Common Debugging Scenarios

### Debugging File Path Resolution

Set breakpoints in:
- `PathResolver.TryResolveInputPath()`
- `PathResolver.TryResolveOutputPath()`
- `ConvertAutoMediaFilesCommand.ProcessFile()` (line 171)

### Debugging Audio Track Mapping

Set breakpoints in:
- `ConvertAutoMediaFilesCommand.CreateAudioTrackMappings()` (line 274)
- `AudioTrackMappingService.ParseChannelCount()`
- `ConvertAutoMediaFilesCommand.ProcessFile()` (line 256-268)

### Debugging Media Conversion

Set breakpoints in:
- `ConvertAutoMediaFilesCommand.ProcessConversion()` (line 346)
- `MediaConversionService.ExecuteConversion()`
- `FfmpegService` methods

### Debugging Error Handling

Set breakpoints in:
- Exception catch blocks in `ProcessFile()` (lines 210, 261, 377)
- `FfmpegConversionException` handling (line 369)
- Error record creation (lines 180, 195, 215, 375, 382)

## Quick Reference

### Launch PowerShell for Debugging
```powershell
.\scripts\Launch.ps1 -Configuration Debug
```

### Build Module
```powershell
.\scripts\Build.ps1 -Configuration Debug -Build -Publish
```

### Run Tests with Debugger
```powershell
dotnet test --filter "FullyQualifiedName~ConvertAutoMediaFilesCommand" --logger "console;verbosity=detailed"
```

### Enable DebuggerService Breakpoints
```powershell
[Dadstart.Labs.MediaForge.Services.System.DebuggerService]::BreakAll = $true
```

## Additional Resources

- [PowerShell Cmdlet Development](https://learn.microsoft.com/en-us/powershell/scripting/developer/cmdlet/)
- [VS Code Debugging](https://code.visualstudio.com/docs/editor/debugging)
- [.NET Debugging](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debugging)
