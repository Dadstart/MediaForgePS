---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Convert-MediaFiles

## SYNOPSIS
{{ Fill in the Synopsis }}

## SYNTAX

### DefaultEncoder (Default)
```
Convert-MediaFiles [-InputPath] <Object[]> [-OutputDirectory] <String> [-DefaultVideoEncoder <String>]
 [-AudioTrackMappings <AudioTrackMapping[]>] [-X265Params <String>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### ExplicitSettings
```
Convert-MediaFiles [-InputPath] <Object[]> [-OutputDirectory] <String>
 -VideoEncodingSettings <VideoEncodingSettings> [-AudioTrackMappings <AudioTrackMapping[]>]
 [-X265Params <String>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
{{ Fill in the Description }}

## EXAMPLES

### Example 1
```powershell
PS C:\> {{ Add example code here }}
```

{{ Add example description here }}

## PARAMETERS

### -AudioTrackMappings
Audio track mappings to use for all files.
If not provided, mappings are automatically detected and created for each file

```yaml
Type: AudioTrackMapping[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DefaultVideoEncoder
Default encoder to use when VideoEncodingSettings is not specified: 'x264' (libx264), 'x265' (libx265), or 'nvenc' (NVENC HEVC)

```yaml
Type: String
Parameter Sets: DefaultEncoder
Aliases:
Accepted values: x264, x265, nvenc

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputPath
Array of input file paths to convert

```yaml
Type: Object[]
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -OutputDirectory
Directory where output files will be written (files keep original name with .mkv extension)

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

### -VideoEncodingSettings
Override default video encoding settings.
If not provided, uses default for DefaultVideoEncoder

```yaml
Type: VideoEncodingSettings
Parameter Sets: ExplicitSettings
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -X265Params
Additional x265 params (passed to ffmpeg via -x265-params)

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
{{ Fill ProgressAction Description }}

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

## OUTPUTS

### Dadstart.Labs.MediaForge.Cmdlets.ConvertMediaFilesCommand+ConversionResult

## NOTES

## RELATED LINKS
