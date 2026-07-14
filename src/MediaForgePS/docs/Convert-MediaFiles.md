---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Convert-MediaFiles

## SYNOPSIS
Converts multiple media files with automatic audio stream selection and configurable video encoding.

## SYNTAX

### DefaultEncoder (Default)
```
Convert-MediaFiles [-InputPath] <Object[]> [-OutputDirectory] <String> [-DefaultVideoEncoder <String>]
 [-AudioTrackMappings <AudioTrackMapping[]>] [-X265Params <String>] [-ProgressAction <ActionPreference>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ExplicitSettings
```
Convert-MediaFiles [-InputPath] <Object[]> [-OutputDirectory] <String>
 -VideoEncodingSettings <VideoEncodingSettings> [-AudioTrackMappings <AudioTrackMapping[]>]
 [-X265Params <String>] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Convert-MediaFiles processes multiple video files: it resolves paths, detects or uses provided audio track mappings, and converts each file using FFmpeg. Output files keep the original base name with `.mp4` extension in the specified `OutputDirectory`.

Default video encoding is chosen by `-DefaultVideoEncoder`: **x264** (libx264, CRF 18, preset medium), **x265** (libx265, CRF 18, preset medium), or **nvenc** (hevc_nvenc, CQ 18, preset p5). When `-DefaultVideoEncoder` is omitted, **x265** is used. Override with `-VideoEncodingSettings` for full control.

If `-AudioTrackMappings` is not provided, mappings are auto-detected per file (preferring English audio, then by codec and channel count). The cmdlet outputs `ConversionResult` objects (`FilePath`, `Success`, `Status`) for each input. Failed files are reported but the batch continues. Duplicate pipeline paths are ignored. Progress reporting includes per-file and batch ETA. Supports -WhatIf and -Confirm.

## EXAMPLES

### Example 1: Convert all MKV files in a folder using x265
```powershell
Convert-MediaFiles -InputPath "C:\Source\*.mkv" -OutputDirectory "C:\Output" -DefaultVideoEncoder x265
```

Converts each MKV under C:\Source to .mp4 in C:\Output using libx265 defaults.

### Example 2: Convert with custom video settings and explicit audio mappings
```powershell
$settings = New-VideoEncodingSettings -Codec libx265 -CRF 20 -Preset fast
$mappings = Get-AudioStreams -InputPath "C:\movie.mkv"
Convert-MediaFiles -InputPath "C:\movie.mkv" -OutputDirectory "C:\Out" -VideoEncodingSettings $settings -AudioTrackMappings $mappings
```

Uses custom CRF 20 and the audio mappings from Get-AudioStreams for the conversion.

### Example 3: Pipeline input from Get-ChildItem
```powershell
Get-ChildItem "C:\Videos" -Filter *.mkv | Convert-MediaFiles -OutputDirectory "C:\Converted" -DefaultVideoEncoder nvenc
```

Converts all MKV files in C:\Videos to C:\Converted using NVENC HEVC.

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
Default encoder when `-VideoEncodingSettings` is not specified: **x264** (libx264), **x265** (libx265), or **nvenc** (NVENC HEVC). When omitted, **x265** is used.

```yaml
Type: String
Parameter Sets: DefaultEncoder
Aliases:
Accepted values: x264, x265, nvenc

Required: False
Position: Named
Default value: x265
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputPath
Array of input file paths to convert. Can be strings or FileSystemInfo objects; accepts pipeline input by value or by property name.

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
Directory where output files are written. Each file keeps its original base name with .mp4 extension.

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

### System.Object[]
Paths to media files (strings or FileSystemInfo). Duplicates are ignored.

## OUTPUTS

### ConversionResult
For each input file: FilePath (original path), Success (boolean), Status (message).

## NOTES
Requires FFmpeg. Output extension is .mp4. Failed files are reported in the output and via WriteError.

## RELATED LINKS

[Convert-VideoFile](Convert-VideoFile.md)
[Convert-MediaFileAdvanced](Convert-MediaFileAdvanced.md)
[Get-MediaFile](Get-MediaFile.md)
[Get-AudioStreams](Get-AudioStreams.md)
[New-VideoEncodingSettings](New-VideoEncodingSettings.md)
[New-AudioTrackMapping](New-AudioTrackMapping.md)
