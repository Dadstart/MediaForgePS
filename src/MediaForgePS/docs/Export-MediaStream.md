---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Export-MediaStream

## SYNOPSIS
Exports a single stream (video, audio, subtitle, or data) from a media file to a separate file without re-encoding.

## SYNTAX

```
Export-MediaStream [-InputPath] <String> [-OutputPath] <String> [-Type] <String> [-Index] <Int32> [-Force]
 [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Export-MediaStream uses FFmpeg to extract one stream from a media file and write it to an output file with stream copy (no re-encoding). -Type can be Video, Audio, Subtitle, Data, or All; -Index is the zero-based stream index within that type (or the absolute stream index when Type is All). If the output file already exists, the cmdlet fails unless -Force is specified. Supports -WhatIf and -Confirm.

## EXAMPLES

### Example 1: Extract first audio stream
```powershell
Export-MediaStream -InputPath "C:\movie.mkv" -OutputPath "C:\Out\audio.aac" -Type Audio -Index 0
```

Extracts the first audio stream to audio.aac.

### Example 2: Extract first subtitle stream with overwrite
```powershell
Export-MediaStream -InputPath "movie.mkv" -OutputPath "subs.srt" -Type Subtitle -Index 0 -Force
```

Extracts the first subtitle stream to subs.srt, overwriting if it exists.

### Example 3: WhatIf
```powershell
Export-MediaStream -InputPath "in.mkv" -OutputPath "out.video" -Type Video -Index 0 -WhatIf
```

Shows what would be done without extracting.

## PARAMETERS

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Overwrites the output file if it already exists

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

### -Index
Zero-based index of the stream within the specified Type (or absolute index when Type is All).

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: True
Position: 3
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputPath
Path to the input media file. Supports pipeline input. Supports relative or absolute paths.

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
Path where the extracted stream will be saved. Use -Force to overwrite if the file exists.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Kind of stream: Video, Audio, Subtitle, Data, or All. With All, -Index is the absolute stream index.

```yaml
Type: String
Parameter Sets: (All)
Aliases:
Accepted values: Video, Audio, Subtitle, Data, All

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

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
Input path (and optionally output path by property name).

## OUTPUTS

### None
This cmdlet does not write to the pipeline.

## NOTES
Requires FFmpeg. Streams are copied without re-encoding. Use Get-MediaFile to inspect stream indices and types.

## RELATED LINKS
[Get-MediaFile](Get-MediaFile.md)
[Export-Subtitles](Export-Subtitles.md)
