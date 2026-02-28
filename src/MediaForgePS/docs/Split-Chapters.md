---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Split-Chapters

## SYNOPSIS
Splits a video file into multiple files based on chapter ranges.

## SYNTAX

### ByRanges (Default)
```
Split-Chapters [-InputFile] <String> [-ChapterRanges] <Object[]> [-OutputPath <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### AllChapters
```
Split-Chapters [-InputFile] <String> [-AllChapters] [-OutputPath <String>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Split-Chapters uses ffprobe to read chapter information and ffmpeg to split the video by time ranges. Chapter indices are 1-based: Start=1, End=1 is the first chapter. Use -ChapterRanges with objects that have Start, End (inclusive), and optional OutputName; or use -AllChapters to split every chapter into its own file. Output files are written to -OutputPath (default: same directory as input) with names like basename.split-01.mkv or the custom OutputName when provided.

## EXAMPLES

### Example 1: Split specific chapter ranges
```powershell
$ranges = @(
    @{ Start = 1; End = 2 }
    @{ Start = 5; End = 7; OutputName = "part2" }
)
Split-Chapters -InputFile "C:\movie.mkv" -ChapterRanges $ranges -OutputPath "C:\Out"
```

Splits chapters 1-2 and 5-7 into separate files; the second range is named part2 plus extension.

### Example 2: Split every chapter into its own file
```powershell
Split-Chapters -InputFile "C:\movie.mkv" -AllChapters
```

Creates one file per chapter in the same directory as the input.

### Example 3: Pipeline input
```powershell
Get-ChildItem "C:\Videos\*.mkv" | Split-Chapters -ChapterRanges (@{ Start=1; End=3 }) -OutputPath "C:\Splits"
```

## PARAMETERS

### -AllChapters
Split every chapter into its own file (mutually exclusive with -ChapterRanges).

```yaml
Type: SwitchParameter
Parameter Sets: AllChapters
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ChapterRanges
Chapter ranges with Start, End (1-based, inclusive) and optional OutputName.

```yaml
Type: Object[]
Parameter Sets: ByRanges
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputFile
Path to the input video file to split.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -OutputPath
Directory where output files are saved; defaults to the input file's directory.

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

### System.String
Path(s) to the input video file(s) (pipeline by value or property name).

## OUTPUTS

### System.String[]
Paths of the created output files.

## NOTES
Requires ffprobe and ffmpeg. Chapter ranges must have at least one valid range with Start and End.

## RELATED LINKS
[Split-SeriesChapters](Split-SeriesChapters.md)
[Get-MediaFile](Get-MediaFile.md)
