---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Get-AudioStreams

## SYNOPSIS
Returns audio track mappings for a media file suitable for use with conversion cmdlets.

## SYNTAX

```
Get-AudioStreams [-InputPath] <String> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Get-AudioStreams reads a media file with ffprobe and returns an array of AudioTrackMapping objects that describe how to map and optionally encode each audio stream. Use the output with Convert-MediaFiles or Convert-MediaFileAdvanced. The mappings are built from the file's audio streams and can be passed as-is or edited (e.g. with New-AudioTrackMapping for custom copy/encode settings).

## EXAMPLES

### Example 1: Get mappings for a file and convert
```powershell
$mappings = Get-AudioStreams -InputPath "C:\movie.mkv"
Convert-MediaFiles -InputPath "C:\movie.mkv" -OutputDirectory "C:\Out" -DefaultVideoEncoder x265 -AudioTrackMappings $mappings
```

Retrieves audio mappings from movie.mkv and uses them for conversion.

### Example 2: Inspect suggested mappings
```powershell
Get-AudioStreams -InputPath "episode.mkv" | Format-Table -Property *
```

Lists all suggested audio track mappings for the file.

## PARAMETERS

### -InputPath
Path to the input media file. Supports pipeline input by value or property name.

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
Path to the media file.

## OUTPUTS

### AudioTrackMapping[]
Array of audio track mappings (copy or encode) for the file's audio streams.

## NOTES
Requires ffprobe. Output is intended for -AudioTrackMappings in Convert-MediaFiles or Convert-MediaFileAdvanced.

## RELATED LINKS

[Convert-MediaFiles](Convert-MediaFiles.md)
[Convert-MediaFileAdvanced](Convert-MediaFileAdvanced.md)
[New-AudioTrackMapping](New-AudioTrackMapping.md)
[Get-MediaFile](Get-MediaFile.md)
