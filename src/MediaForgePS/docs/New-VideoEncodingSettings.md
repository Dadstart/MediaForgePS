---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# New-VideoEncodingSettings

## SYNOPSIS
{{ Fill in the Synopsis }}

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
{{ Fill in the Description }}

## EXAMPLES

### Example 1
```powershell
PS C:\> {{ Add example code here }}
```

{{ Add example description here }}

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
The Constant Rate Factor value for quality control.
Lower values indicate higher quality.
Typical ranges: 18-28 for H.264, 20-30 for H.265

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

### None

## OUTPUTS

### Dadstart.Labs.MediaForge.Models.VideoEncodingSettings

## NOTES

## RELATED LINKS
