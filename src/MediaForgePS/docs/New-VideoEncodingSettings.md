---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# New-VideoEncodingSettings

## SYNOPSIS
Creates a VideoEncodingSettings object for use with conversion cmdlets.

## SYNTAX

### CRF (Default)
```
New-VideoEncodingSettings -Codec <String> -CRF <Int32> [-Preset <String>] [-CodecProfile <String>]
 [-Tune <String>] [-PixelFormat <String>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### VBR
```
New-VideoEncodingSettings -Codec <String> -Bitrate <Int32> [-Preset <String>] [-PixelFormat <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
New-VideoEncodingSettings creates an object that holds video encoding parameters: codec, CRF or bitrate, preset, profile, tune, and pixel format. Use the **CRF** parameter set for constant quality (CRF range 0-51; typical: 18-28 for H.264, 20-30 for H.265) or the **VBR** parameter set for variable bitrate.

The result is passed to `Convert-MediaFileAdvanced` or `Convert-MediaFiles` via `-VideoEncodingSettings`. Default pixel format is `yuv420p` for libx264 and `yuv420p10le` for libx265 if `-PixelFormat` is omitted. Defaults: preset **slow**, profile **high**, tune **film** (CRF set only).

Supported codecs include `libx264`, `libx265`, and `vp9`. For NVENC (`hevc_nvenc`), use `-DefaultVideoEncoder nvenc` on `Convert-VideoFile`, `Convert-MediaFiles`, or `Invoke-BonusFileProcessing` instead of this cmdlet.

## EXAMPLES

### Example 1: H.265 with CRF
```powershell
New-VideoEncodingSettings -Codec libx265 -CRF 20 -Preset slow -CodecProfile main
```

Creates settings for libx265, CRF 20, slow preset, main profile.

### Example 2: H.264 with bitrate
```powershell
New-VideoEncodingSettings -Codec libx264 -Bitrate 5000 -Preset medium
```

Creates variable bitrate settings for libx264 at 5000 kbps.

### Example 3: Use with Convert-MediaFileAdvanced
```powershell
$settings = New-VideoEncodingSettings -Codec libx265 -CRF 18 -Preset medium
Convert-MediaFileAdvanced -InputPath "in.mkv" -OutputPath "out.mp4" -VideoEncodingSettings $settings -AudioTrackMappings $mappings
```

## PARAMETERS

### -Bitrate
The bitrate for variable bitrate encoding in kbps

```yaml
Type: Int32
Parameter Sets: VBR
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CRF
The Constant Rate Factor value for quality control (0-51). Lower values indicate higher quality. Typical ranges: 18-28 for H.264, 20-30 for H.265.

```yaml
Type: Int32
Parameter Sets: CRF
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Codec
The video codec to use for encoding (e.g., 'libx264', 'libx265', 'vp9')

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CodecProfile
The codec profile to use (e.g., 'high', 'main', 'baseline' for H.264)

```yaml
Type: String
Parameter Sets: CRF
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PixelFormat
The pixel format to use for encoding (e.g., 'yuv420p', 'yuv420p10le').
Defaults to 'yuv420p10le' for libx265 and 'yuv420p' for libx264

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

### -Preset
The encoding preset that balances speed vs.
compression efficiency (e.g., 'ultrafast', 'superfast', 'veryfast', 'faster', 'fast', 'medium', 'slow', 'slower', 'veryslow')

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

### -Tune
The tuning option for the codec (e.g., 'film', 'animation', 'grain', 'stillimage', 'fastdecode', 'zerolatency')

```yaml
Type: String
Parameter Sets: CRF
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

### None
This cmdlet does not accept pipeline input.

## OUTPUTS

### VideoEncodingSettings
ConstantRateVideoEncodingSettings or VariableRateVideoEncodingSettings (CRF or VBR parameter set).

## NOTES
Preset default is **slow**. CodecProfile default is **high**; Tune default is **film** (CRF set only). Valid codecs include libx264, libx265, and vp9. NVENC encoding is not available through this cmdlet; use `-DefaultVideoEncoder nvenc` on batch conversion cmdlets.

## RELATED LINKS

[Convert-MediaFiles](Convert-MediaFiles.md)
[Convert-MediaFileAdvanced](Convert-MediaFileAdvanced.md)
