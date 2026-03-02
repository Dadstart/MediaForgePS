---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Convert-ImageSubtitlesToSrt

## SYNOPSIS
Converts image-based subtitle files (SUP, SUB) to SRT using Subtitle Edit with Tesseract OCR.

## SYNTAX

```
Convert-ImageSubtitlesToSrt [-InputPath] <String[]> [[-OutputPath] <String>] [-Recurse]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Convert-ImageSubtitlesToSrt turns SUP and SUB (image-based) subtitle files into SRT text files using Subtitle Edit and Tesseract OCR. Input can be a single file, multiple files, or a directory; with -Recurse, subdirectories are searched. For a single file you can specify -OutputPath to write the SRT elsewhere; otherwise the SRT is written next to each source file. Output SRT paths are written to the pipeline. Subtitle Edit must be installed in %ProgramFiles%\Subtitle Edit.

## EXAMPLES

### Example 1: Convert a single SUP file
```powershell
Convert-ImageSubtitlesToSrt -InputPath "C:\Subtitles\movie.eng.sup"
```

Produces movie.eng.srt in the same folder and writes the path to the pipeline.

### Example 2: Convert all image subtitles in a folder recursively
```powershell
Convert-ImageSubtitlesToSrt -InputPath "C:\Season1" -Recurse
```

Finds all .sup and .sub files under C:\Season1 and writes .srt files alongside each.

### Example 3: Convert and write to a specific path
```powershell
Convert-ImageSubtitlesToSrt -InputPath "episode.sup" -OutputPath "C:\Out\episode.srt"
```

Converts episode.sup and saves the SRT to C:\Out\episode.srt.

## PARAMETERS

### -InputPath
Path to .sup/.sub file(s) or directory containing .sup/.sub files.

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
Omit to write .srt next to the source file.

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
Paths to .sup or .sub files, or directories containing them. Accepts pipeline input. Alias: Path.

## OUTPUTS

### System.String
Paths of the created SRT files.

## NOTES
Requires Subtitle Edit in %ProgramFiles%\Subtitle Edit and Tesseract OCR. Use Repair-Subtitles afterward to fix common OCR errors in the SRT.

## RELATED LINKS

[Repair-Subtitles](Repair-Subtitles.md)
[Export-Subtitles](Export-Subtitles.md)
[Invoke-SubtitleOcrRepair](Invoke-SubtitleOcrRepair.md)
