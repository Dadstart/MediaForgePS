---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Repair-Subtitles

## SYNOPSIS
Fixes common OCR errors in SRT subtitle files (e.g. music note misreads, pipe as I, unmatched brackets).

## SYNTAX

```
Repair-Subtitles [-InputPath] <String[]> [[-OutputPath] <String>] [-Recurse] [-BackupPath <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Repair-Subtitles corrects typical OCR mistakes in SRT files: music note (♪) misreads, pipe characters read as I, unmatched brackets, and similar. You can pass a single SRT path, multiple paths, or a directory of .srt files. For a single file, use -OutputPath to write to a different file; otherwise the file is overwritten in place. For directories, all .srt files are repaired in place unless you use -BackupPath to copy originals first (directory structure under each input path is preserved).

## EXAMPLES

### Example 1: Repair a single SRT file in place
```powershell
Repair-Subtitles -InputPath "C:\Subtitles\movie.eng.srt"
```

Fixes OCR errors in movie.eng.srt and overwrites the file.

### Example 2: Repair and write to a new file
```powershell
Repair-Subtitles -InputPath "movie.eng.srt" -OutputPath "movie.eng.repaired.srt"
```

Repairs the SRT and writes the result to movie.eng.repaired.srt.

### Example 3: Repair all SRT files in a directory with backup
```powershell
Repair-Subtitles -InputPath "C:\Season1\Subtitles" -Recurse -BackupPath "C:\Backup\srts"
```

Finds all .srt files under C:\Season1\Subtitles (including subdirectories), copies them to C:\Backup\srts preserving structure, then repairs in place.

## PARAMETERS

### -BackupPath
Directory to copy all files to before repairing; preserves directory structure.

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
Path to SRT file(s) or directory containing .srt files.

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

### -OutputPath
Output path when processing a single file.
Omit to overwrite in place.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 1
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
Paths to SRT file(s) or directory/directories containing .srt files. Accepts pipeline input.

## OUTPUTS

### System.String
Paths of repaired SRT files are written to the pipeline.

## NOTES
Only .srt files are processed. For image-based subtitles (SUP, SUB), use Convert-ImageSubtitlesToSrt or Export-Subtitles -Ocr first.

## RELATED LINKS
[Export-Subtitles](Export-Subtitles.md)
[Convert-ImageSubtitlesToSrt](Convert-ImageSubtitlesToSrt.md)
[Invoke-SubtitleOcrRepair](Invoke-SubtitleOcrRepair.md)
