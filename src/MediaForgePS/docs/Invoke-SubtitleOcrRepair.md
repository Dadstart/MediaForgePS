---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Invoke-SubtitleOcrRepair

## SYNOPSIS
Converts image-based subtitle files (SUP, SUB) to SRT via OCR, then repairs the OCR-produced SRT files.

## SYNTAX

```
Invoke-SubtitleOcrRepair [-InputPath] <String[]> [-BackupPath <String>] [-ThrottleLimit <Int32>] [-SkipRepair]
 [-Recurse] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Invoke-SubtitleOcrRepair runs the OCR and repair workflow for subtitles already on disk: it converts SUP and SUB files to SRT using Subtitle Edit and Tesseract, then repairs **only the OCR-produced SRT files** unless `-SkipRepair` is specified. Pre-existing SRT files in the input are not repaired.

Input can be file path(s) or directory/directories; `-Recurse` searches subdirectories. Use `-BackupPath` to copy SRT files to a backup location before repairing (structure preserved). OCR conversions run in parallel up to `-ThrottleLimit` (default 10).

## EXAMPLES

### Example 1: Convert and repair all subtitles in a folder
```powershell
Invoke-SubtitleOcrRepair -InputPath "C:\Season1\Subtitles" -Recurse
```

Converts .sup/.sub to .srt and repairs the OCR-produced SRT files under C:\Season1\Subtitles.

### Example 2: Convert only, skip repair
```powershell
Invoke-SubtitleOcrRepair -InputPath "C:\Subs" -SkipRepair
```

Converts image subtitles to SRT but does not run the repair step.

### Example 3: With backup before repair
```powershell
Invoke-SubtitleOcrRepair -InputPath "C:\Subs" -BackupPath "C:\Backup\srts" -ThrottleLimit 5
```

Backs up SRT files to C:\Backup\srts, then converts and repairs; limits parallel OCR conversions to 5.

## PARAMETERS

### -BackupPath
Directory to copy SRT files to before repairing; preserves path structure.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputPath
Path(s) to SUP, SUB, or SRT file(s) or directory/directories containing them.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: Path

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -SkipRepair
Skip SRT repair; only convert image subtitles to SRT.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recurse
When input is a directory, recurse into subdirectories.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ThrottleLimit
Maximum number of image-to-SRT conversions to run in parallel. Default is 10.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 10
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction
Specifies how the cmdlet responds to progress updates. Use SilentlyContinue to hide progress.

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]
Paths to .sup, .sub, or .srt files, or directories containing them. Alias: Path.

## OUTPUTS

### None
This cmdlet does not write to the pipeline.

## NOTES
Requires Subtitle Edit and Tesseract when any input is SUP or SUB.

## RELATED LINKS

[Convert-ImageSubtitlesToSrt](Convert-ImageSubtitlesToSrt.md)
[Repair-Subtitles](Repair-Subtitles.md)
[Export-Subtitles](Export-Subtitles.md)
