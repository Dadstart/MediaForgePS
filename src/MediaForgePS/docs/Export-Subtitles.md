---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Export-Subtitles

## SYNOPSIS
Exports English subtitle streams from media files and converts image subtitles to SRT via OCR unless skipped.

## SYNTAX

```
Export-Subtitles [-InputPath] <Object[]> [-BackupPath <String>] [-ThrottleLimit <Int32>] [-SkipOcr] [-SkipRepair]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Export-Subtitles extracts English subtitle tracks from media files (MKV and others). For each file it finds subtitle streams whose language matches English and exports them next to the source file with an appropriate extension (e.g. .srt, .sup).

Unless you specify -SkipOcr, the cmdlet also converts image-based subtitle files (SUP, SUB) to SRT using Subtitle Edit with Tesseract OCR, then repairs the SRT text (fixes common OCR errors) unless -SkipRepair is specified. Use -BackupPath to copy SRT files to a backup location before repairing. Output SRT paths are written to the pipeline when OCR processing runs. InputPath can be media file path(s), folder path(s) containing .mkv files, or MediaFile objects from Get-MediaFile.

## EXAMPLES

### Example 1: Export English subtitles from a single file
```powershell
Export-Subtitles -InputPath "C:\Videos\movie.mkv"
```

Exports all English subtitle streams from movie.mkv to files alongside the video (e.g. movie.eng.srt).

### Example 2: Export from a folder and convert image subtitles to SRT with repair
```powershell
Get-ChildItem "C:\Videos" -Filter *.mkv | Export-Subtitles -BackupPath "C:\Backup\srts"
```

Exports subtitles from all MKV files in C:\Videos. Image-based tracks (SUP/SUB) are converted to SRT via OCR, SRT files are backed up to C:\Backup\srts (structure preserved), then repaired. Resulting SRT paths are emitted to the pipeline.

### Example 3: Export and convert without SRT repair
```powershell
Export-Subtitles -InputPath "C:\Videos\season1" -SkipRepair
```

Exports and converts image subtitles to SRT but skips the repair step.

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
Path(s) to media file(s) or folder(s) containing .mkv files.

```yaml
Type: Object[]
Parameter Sets: (All)
Aliases: Path

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -SkipRepair
Skip SRT repair during default OCR processing.

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

### -SkipOcr
Skip OCR conversion of image subtitles to SRT.

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
Maximum number of image-to-SRT conversions to run in parallel. Only applies unless -SkipOcr is specified. Default is 10.

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
Specifies how the cmdlet responds to progress updates (e.g. Write-Progress). Use SilentlyContinue to hide progress.

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

### System.Object[]
Path strings, folder paths, or MediaFile objects (e.g. from Get-MediaFile). For folders, all .mkv files under the path are processed.

## OUTPUTS

### System.String
When OCR processing runs, the paths of exported or repaired SRT files are written to the pipeline.

## NOTES
Requires mkvextract for extracting embedded subtitles. When OCR processing is enabled, Subtitle Edit and Tesseract must be installed (Subtitle Edit expected under %ProgramFiles%\Subtitle Edit).

## RELATED LINKS

[Get-MediaFile](Get-MediaFile.md)
[Repair-Subtitles](Repair-Subtitles.md)
[Convert-ImageSubtitlesToSrt](Convert-ImageSubtitlesToSrt.md)
[Invoke-SubtitleOcrRepair](Invoke-SubtitleOcrRepair.md)
