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
Invoke-SeasonScan fetches TVDb episode metadata for the given `-Season`. Provide `-TvDbSeriesUrl` and optionally `-TvDbSeasonUrl`; when `TvDbSeasonUrl` is omitted, it is constructed from the series URL and season number.

Output is an array of `TvDbEpisodeInfo` objects with these properties:

| Property | Description |
|----------|-------------|
| `Id` | TVDb episode ID (used in output file names) |
| `SeasonNumber` | Season number |
| `Title` | Episode title |
| `EpisodeNumber` | Episode number within the season |

Use the output with `Invoke-VideoCopy`, `Split-SeriesChapters`, or `Invoke-SeriesProcessing`. Requires network access to TheTVDB API and a configured `TVDB_API_KEY` environment variable.

## EXAMPLES

### Example 1: Scan by series slug
```powershell
$env:TVDB_API_KEY = 'your-key-here'   # once per session if not already set

Invoke-SeasonScan -Season 1 -TvDbSeriesUrl "https://thetvdb.com/series/breaking-bad"
```

Returns `TvDbEpisodeInfo` objects for season 1 using the series slug. Set `TVDB_API_KEY` (and `TVDB_PIN` when required) before calling.

### Example 2: Scan by numeric series ID
```powershell
Invoke-SeasonScan -Season 1 -TvDbSeriesUrl "https://thetvdb.com/series/81189"
```

Same result using TheTVDB numeric series ID in the URL.

### Example 3: Scan with an explicit season URL
```powershell
Invoke-SeasonScan -Season 1 -TvDbSeasonUrl "https://thetvdb.com/series/breaking-bad/seasons/official/1"
```

Uses the season-order path (`official`, `dvd`, and so on) and season number from the URL.

### Example 4: Pass to Invoke-VideoCopy
```powershell
$episodes = Invoke-SeasonScan -Season 2 -TvDbSeriesUrl "https://thetvdb.com/series/81189"
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
Requires network access to TheTVDB API (`api4.thetvdb.com`). Set the `TVDB_API_KEY` environment variable (and `TVDB_PIN` when using a user-supported key). If no episode information is returned, the cmdlet writes a warning and produces no output. At least one of `-TvDbSeriesUrl` or `-TvDbSeasonUrl` should be provided.

## RELATED LINKS

[Invoke-VideoCopy](Invoke-VideoCopy.md)
[Invoke-SeriesProcessing](Invoke-SeriesProcessing.md)
[Split-SeriesChapters](Split-SeriesChapters.md)
