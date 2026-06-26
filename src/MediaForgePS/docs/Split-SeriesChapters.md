---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Split-SeriesChapters

## SYNOPSIS
Splits a video file into episode files by chapter ranges and names them using TVDb episode metadata.

## SYNTAX

```
Split-SeriesChapters -Title <String> -Season <Int32> [-EpisodeStart <Int32>] [-InputFile] <String>
 [-ChapterRanges] <Object[]> [-OutputPath <String>] [-TvDbSeriesUrl <String>] [-TvDbSeasonUrl <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Split-SeriesChapters splits one video (e.g. a combined season file) into multiple episode files by chapter ranges. Each range maps to an episode; output file names follow a Plex-friendly pattern: `{Title} {tvdb ID} S{season}E{episode}.{ext}` (e.g. `Show Name {tvdb 12345} S01E01.mkv`).

TVDb metadata is fetched using `Invoke-SeasonScan` (`-TvDbSeriesUrl`, `-TvDbSeasonUrl`). `-EpisodeStart` is the episode number for the first range. If a range has `OutputName`, that name is used instead of the TVDb-based name. The cmdlet requires at least `(EpisodeStart - 1) + rangeCount` episodes from the TVDb scan.

## EXAMPLES

### Example 1: Split combined file into episodes with TVDb naming
```powershell
$ranges = @(
    @{ Start = 1; End = 1 },
    @{ Start = 2; End = 2 },
    @{ Start = 3; End = 3 }
)
Split-SeriesChapters -Title "My Show" -Season 1 -InputFile "C:\season1.mkv" -ChapterRanges $ranges -TvDbSeriesUrl "https://thetvdb.com/series/12345"
```

Splits the file into three episode files named with TVDb IDs and S01E01, S01E02, S01E03.

### Example 2: Mid-season start with custom output directory
```powershell
Split-SeriesChapters -Title "Show" -Season 2 -EpisodeStart 5 -InputFile "s2.mkv" `
    -ChapterRanges (@{Start=1;End=2}, @{Start=3;End=4}) -OutputPath "C:\Output" `
    -TvDbSeriesUrl "https://thetvdb.com/series/12345"
```

Maps the first range to episode 5 and the second to episode 6.

## PARAMETERS

### -ChapterRanges
Chapter ranges with Start, End (1-based, inclusive) and optional OutputName.

```yaml
Type: Object[]
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EpisodeStart
First episode number mapped to the first chapter range (default 1).

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

### -InputFile
Input video file to split into episodes.

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
Output directory for episode files; defaults to the input file's directory when omitted.

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

### -Season
Season number represented by the input file (1-based).

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
Series title used in output file names.

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
Optional TVDb series URL used as a starting point for fetching episode metadata.

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

### System.String
Path to the input video file (pipeline by value or property name).

## OUTPUTS

### System.String[]
Paths of the created episode files.

## NOTES
Requires ffprobe, ffmpeg, and TVDb episode data. Use Invoke-SeasonScan to verify episode count before splitting.

## RELATED LINKS

[Split-Chapters](Split-Chapters.md)
[Invoke-SeasonScan](Invoke-SeasonScan.md)
[Invoke-SeriesProcessing](Invoke-SeriesProcessing.md)
