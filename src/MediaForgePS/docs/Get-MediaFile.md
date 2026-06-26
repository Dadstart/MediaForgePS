---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Get-MediaFile

## SYNOPSIS
Retrieves detailed information about a media file, including format, streams, and chapters.

## SYNTAX

```
Get-MediaFile [-Path] <String> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Get-MediaFile uses ffprobe to analyze a media file and returns a `MediaFile` object with:

- **Path** - resolved file path
- **Format** - container format metadata (duration, bit rate, format name)
- **Streams** - video, audio, subtitle, and other streams (index, codec, language, tags)
- **Chapters** - chapter markers with start/end times

Path can be relative or absolute. Use the output with other MediaForge cmdlets such as `Export-Subtitles` or `Convert-MediaFiles`, or pipe `MediaFile` objects directly to `Export-Subtitles`.

## EXAMPLES

### Example 1: Get metadata for a single file
```powershell
Get-MediaFile -Path "C:\Videos\movie.mkv"
```

Returns a MediaFile object with format, streams, and chapters for movie.mkv.

### Example 2: Pipe paths from Get-ChildItem
```powershell
Get-ChildItem "C:\Videos" -Filter *.mkv | Get-MediaFile
```

Outputs MediaFile objects for each MKV file in C:\Videos.

### Example 3: Inspect streams before conversion
```powershell
$mf = Get-MediaFile ".\episode.mkv"
$mf.Streams | Where-Object { $_.Type -eq "audio" } | Format-Table Index, Codec, Language
```

Retrieves media info and lists audio streams in a table.

## PARAMETERS

### -Path
Path to the media file to analyze. Can be relative or absolute.

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
Path to one or more media files (when piped).

## OUTPUTS

### Dadstart.Labs.MediaForge.Models.MediaFile
MediaFile object with format, streams, and chapters.

## NOTES
Requires ffprobe (FFmpeg) to be available on the path or in the module's expected location.

## RELATED LINKS

[Export-Subtitles](Export-Subtitles.md)
[Convert-MediaFiles](Convert-MediaFiles.md)
[Get-AudioStreams](Get-AudioStreams.md)
