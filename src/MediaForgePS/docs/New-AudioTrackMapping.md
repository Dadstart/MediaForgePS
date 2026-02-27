---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# New-AudioTrackMapping

## SYNOPSIS
{{ Fill in the Synopsis }}

## SYNTAX

### Copy (Default)
```
New-AudioTrackMapping [-Title <String>] [-SourceStream] <Int32> [-SourceIndex] <Int32>
 [-DestinationIndex] <Int32> [-Copy] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Encode
```
New-AudioTrackMapping [-Title <String>] [-SourceStream] <Int32> [-SourceIndex] <Int32>
 [-DestinationIndex] <Int32> [-Encode] -Codec <String> [-Bitrate <Int32>] -Channels <Int32>
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
Destination bitrate in kbps.
If not specified, defaults are used based on channel count

```yaml
Type: Int32
Parameter Sets: Encode
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Channels
Number of audio channels for the encoded output (e.g., 1 for mono, 2 for stereo, 6 for 5.1, 8 for 7.1)

```yaml
Type: Int32
Parameter Sets: Encode
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Codec
Destination codec for encoding (e.g., 'aac', 'mp3', 'opus')

```yaml
Type: String
Parameter Sets: Encode
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Copy
Creates a copy mapping that copies the audio stream without re-encoding

```yaml
Type: SwitchParameter
Parameter Sets: Copy
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DestinationIndex
Destination audio stream index in the output file

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Encode
Creates an encode mapping that encodes the audio stream with specified settings

```yaml
Type: SwitchParameter
Parameter Sets: Encode
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SourceIndex
Source audio stream index within the source stream

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SourceStream
Source stream index (typically 0 for the input file)

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Title
Title metadata for the audio track

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

### None

## OUTPUTS

### Dadstart.Labs.MediaForge.Models.AudioTrackMapping

## NOTES

## RELATED LINKS
