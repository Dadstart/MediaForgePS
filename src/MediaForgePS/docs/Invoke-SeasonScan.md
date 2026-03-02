---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Invoke-SeasonScan

## SYNOPSIS
Retrieves TVDb episode information for a season.

## SYNTAX

```
Invoke-SeasonScan -Season <Int32> [-TvDbSeriesUrl <String>] [-TvDbSeasonUrl <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Invoke-SeasonScan calls the series processing service to fetch TVDb episode metadata for the given -Season. Use -TvDbSeriesUrl and optionally -TvDbSeasonUrl to point at the series and season; if TvDbSeasonUrl is omitted, it is built from TvDbSeriesUrl and Season. Output is an array of TvDbEpisodeInfo objects (Id, EpisodeNumber, etc.) used by Invoke-VideoCopy and Split-SeriesChapters.

## EXAMPLES

### Example 1: Get episode list for a season
```powershell
Invoke-SeasonScan -Season 1 -TvDbSeriesUrl "https://thetvdb.com/series/12345"
```

Returns TvDbEpisodeInfo objects for season 1.

### Example 2: Pass to Invoke-VideoCopy
```powershell
$episodes = Invoke-SeasonScan -Season 2 -TvDbSeriesUrl "https://thetvdb.com/series/12345"
Invoke-VideoCopy -Title "Show" -Season 2 -Path "C:\Source" -FilePatterns "*.mkv" -Destination "P:\Season2" -Episodes $episodes
```

Scans season 2 and uses the result for video copy.

## PARAMETERS

### -Season
Season number to scan (1-based).

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

### -TvDbSeasonUrl
Optional TVDb season URL; when omitted, constructed from TvDbSeriesUrl and Season.

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

### -TvDbSeriesUrl
Optional TVDb series URL used as a starting point for the scan.

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
Parameters are specified directly.

## OUTPUTS

### TvDbEpisodeInfo[]
Array of episode metadata for the season.

## NOTES
If no episode information is returned, the cmdlet writes a warning and produces no output.

## RELATED LINKS

[Invoke-VideoCopy](Invoke-VideoCopy.md)
[Invoke-SeriesProcessing](Invoke-SeriesProcessing.md)
[Split-SeriesChapters](Split-SeriesChapters.md)
