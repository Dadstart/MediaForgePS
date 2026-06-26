---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Export-Subtitles

## SYNOPSIS
Exports English subtitle streams from media files. Use -Ocr Auto, Skip, or Force to control image subtitle OCR and SRT repair.

## SYNTAX

```
Export-Subtitles [-InputPath] <Object[]> [-BackupPath <String>] [-ThrottleLimit <Int32>] [-Ocr <String>]
 [-SkipRepair] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Export-Subtitles extracts English subtitle tracks from media files (MKV and others). For each file it finds subtitle streams whose language matches English and exports them next to the source file with an appropriate extension (e.g. .srt, .sup).

Use `-Ocr` to control post-extraction OCR and repair. Accepted values are **Auto** (default), **Skip**, and **Force**:

- **Auto** - OCR image subtitles only when the source has a single exported subtitle format and it is not SRT.
- **Force** - OCR all exported image subtitle files.
- **Skip** - extract only; no OCR or repair.

When OCR runs, only **OCR-produced** SRT files are repaired (native exported SRT files are not repaired). Use `-BackupPath` to copy SRT files to a backup location before repairing. Use `-SkipRepair` to skip the repair step.

InputPath can be media file path(s), folder path(s) containing `.mkv` files, or `MediaFile` objects from `Get-MediaFile`. This cmdlet has alias **Export-RepairedSubtitles**.

## EXAMPLES

### Example 1: Export English subtitles from a single file
```powershell
Export-Subtitles -InputPath "C:\Videos\movie.mkv"
```

Exports all English subtitle streams from movie.mkv to files alongside the video (e.g. movie.eng.srt).

### Example 2: Export from a folder and force OCR with repair
```powershell
Get-ChildItem "C:\Videos" -Filter *.mkv | Export-Subtitles -Ocr Force -BackupPath "C:\Backup\srts"
```

Exports subtitles from all MKV files in C:\Videos. All image-based tracks (SUP/SUB) are converted to SRT via OCR, SRT files are backed up to C:\Backup\srts (structure preserved), then OCR-produced SRT files are repaired.

### Example 3: Export and convert without SRT repair
```powershell
Export-Subtitles -InputPath "C:\Videos\season1" -Ocr Force -SkipRepair
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
Skip SRT repair during OCR processing. Has no effect when -Ocr is not specified.

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
Maximum number of image-to-SRT conversions to run in parallel. Only applies when -Ocr is specified. Default is 10.

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

### -Ocr
Controls OCR of image-based subtitles after extraction. Default is Auto.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: Auto
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Object[]
Path strings, folder paths, or MediaFile objects (e.g. from Get-MediaFile). For folders, all .mkv files under the path are processed.

## OUTPUTS

### None
This cmdlet does not write to the pipeline.

## NOTES
Alias: **Export-RepairedSubtitles**. Requires mkvextract for extracting embedded subtitles from Matroska VobSub tracks. When OCR processing is enabled (`-Ocr Auto` or `Force`), Subtitle Edit and Tesseract must be installed (Subtitle Edit expected under %ProgramFiles%\Subtitle Edit). Folder input processes `*.mkv` files only.

## RELATED LINKS

[Get-MediaFile](Get-MediaFile.md)
[Repair-Subtitles](Repair-Subtitles.md)
[Convert-ImageSubtitlesToSrt](Convert-ImageSubtitlesToSrt.md)
[Invoke-SubtitleOcrRepair](Invoke-SubtitleOcrRepair.md)
