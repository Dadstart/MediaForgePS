---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Convert-MediaFileAdvanced

## SYNOPSIS
Converts a single media file using explicit video encoding settings and audio track mappings.

## SYNTAX

```
Convert-MediaFileAdvanced [-InputPath] <String> [-OutputPath] <String>
 -VideoEncodingSettings <VideoEncodingSettings> -AudioTrackMappings <AudioTrackMapping[]>
 [-AdditionalArguments <String[]>] [-X265Params <String>] [-ProgressAction <ActionPreference>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Convert-MediaFileAdvanced uses FFmpeg to convert one media file with full control: you must supply VideoEncodingSettings (from New-VideoEncodingSettings) and AudioTrackMappings (from Get-AudioStreams or New-AudioTrackMapping). Optional -AdditionalArguments pass extra FFmpeg options; -X265Params are passed via -x265-params when the codec is x265. Use this cmdlet when you need precise control; for batch conversion with auto-detection use Convert-MediaFiles. Supports -WhatIf and -Confirm.

## EXAMPLES

### Example 1: Convert with custom settings and audio mappings
```powershell
$settings = New-VideoEncodingSettings -Codec libx265 -CRF 20 -Preset slow
$mappings = Get-AudioStreams -InputPath "C:\movie.mkv"
Convert-MediaFileAdvanced -InputPath "C:\movie.mkv" -OutputPath "C:\Out\movie.mp4" -VideoEncodingSettings $settings -AudioTrackMappings $mappings
```

Converts movie.mkv to movie.mp4 using the specified encoding and audio mappings.

### Example 2: Add extra x265 parameters
```powershell
$settings = New-VideoEncodingSettings -Codec libx265 -CRF 18 -Preset medium
$mappings = Get-AudioStreams -InputPath "input.mkv"
Convert-MediaFileAdvanced -InputPath "input.mkv" -OutputPath "output.mp4" -VideoEncodingSettings $settings -AudioTrackMappings $mappings -X265Params "aq-mode=3"
```

Passes aq-mode=3 to x265 via -x265-params.

## PARAMETERS

### -AdditionalArguments
Additional Ffmpeg arguments (e.g., codec options, quality settings)

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AudioTrackMappings
Audio track mappings to use for the conversion

```yaml
Type: AudioTrackMapping[]
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputPath
Path to the input media file. Supports relative or absolute paths and PowerShell path resolution.

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
Path to the output media file. Can be relative or absolute.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -VideoEncodingSettings
Video encoding settings to use for the conversion

```yaml
Type: VideoEncodingSettings
Parameter Sets: (All)
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
Input and output paths (by value or property name).

## OUTPUTS

### MediaConversionResult
Written on successful conversion: InputPath, OutputPath, Status, InputSizeMegabytes, OutputSizeMegabytes, SizeReductionPercent, and ProcessingTime. Errors are reported via WriteError.

## NOTES
Requires FFmpeg. For batch conversion with automatic audio detection, use Convert-MediaFiles.

## RELATED LINKS

[Convert-MediaFiles](Convert-MediaFiles.md)
[New-VideoEncodingSettings](New-VideoEncodingSettings.md)
[Get-AudioStreams](Get-AudioStreams.md)
[New-AudioTrackMapping](New-AudioTrackMapping.md)
