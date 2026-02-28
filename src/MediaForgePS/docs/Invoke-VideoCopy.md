---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Invoke-VideoCopy

## SYNOPSIS
Copies episode video files into a destination folder using TVDb episode metadata for naming.

## SYNTAX

```
Invoke-VideoCopy -Title <String> -Season <Int32> [-EpisodeStart <Int32>] -Path <String[]>
 -FilePatterns <String[]> [-MinimumFileSize <Int64>] -Destination <String> -Episodes <TvDbEpisodeInfo[]>
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Invoke-VideoCopy is the lower-level copy step used by Invoke-SeriesProcessing. It searches one or more -Path roots for files matching -FilePatterns, filters by -MinimumFileSize, associates them with -Episodes (TvDbEpisodeInfo from Invoke-SeasonScan), and copies them to -Destination with series title and episode-based naming. -EpisodeStart is the episode number for the first matched file. Pipeline input is accepted for -Path. Outputs the paths of copied files.

## EXAMPLES

### Example 1: Copy with TVDb metadata from season scan
```powershell
$episodes = Invoke-SeasonScan -Season 1 -TvDbSeriesUrl "https://thetvdb.com/series/12345"
Invoke-VideoCopy -Title "My Show" -Season 1 -Path "C:\Source" -FilePatterns "*.mkv" -Destination "P:\TV\My Show\Season 01" -Episodes $episodes
```

Scans TVDb for season 1, then copies matching MKV files to the destination with episode naming.

### Example 2: Pipeline paths
```powershell
"C:\Source1", "C:\Source2" | Invoke-VideoCopy -Title "Show" -Season 2 -FilePatterns "*.mkv" -Destination "P:\Season2" -Episodes $episodes
```

Copies from multiple source folders.

## PARAMETERS

### -Destination
Destination directory where copied episode files are written.

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

### -EpisodeStart
First episode number in the input set (default 1).

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Episodes
TVDb episode metadata for the season, used to name and organize copied files.

```yaml
Type: TvDbEpisodeInfo[]
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FilePatterns
File name patterns (wildcards) used to find episode files under Path.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinimumFileSize
Minimum file size in bytes required to treat a file as an episode (default 1 GB).

```yaml
Type: Int64
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Root folder(s) containing source video files.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Season
Season number to copy (1-based).

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Title
Series title used for destination file naming.

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

### System.String[]
Root folder path(s) to search for video files. Accepts pipeline input by value or property name.

## OUTPUTS

### System.String
Path(s) of copied files (enumerated).

## NOTES
Typically used with Invoke-SeasonScan to obtain -Episodes. Invoke-SeriesProcessing runs this step internally.

## RELATED LINKS
[Invoke-SeasonScan](Invoke-SeasonScan.md)
[Invoke-SeriesProcessing](Invoke-SeriesProcessing.md)
