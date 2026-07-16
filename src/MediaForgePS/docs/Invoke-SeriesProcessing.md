---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Invoke-SeriesProcessing

## SYNOPSIS
Runs the full season workflow: create folders, scan TVDb, copy episodes, and optionally extract chapters and captions.

## SYNTAX

```
Invoke-SeriesProcessing -Title <String> -Season <Int32> [-EpisodeStart <Int32>] -InputPath <String[]>
 -FilePatterns <String[]> [-MinimumFileSize <Int64>] [-OutputPath <String>] [-TvDbSeriesUrl <String>]
 [-TvDbSeasonUrl <String>] [-ExtractChapters] [-SkipCaptionExtraction] [-Ocr <String>] [-SkipRepair]
 [-KeepSource] [-Alert] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Invoke-SeriesProcessing is a high-level workflow that runs five steps in order:

1. **Create folders** - When `-OutputPath` is set, creates `OutputPath\Title\Season XX`.
2. **Scan TVDb** - Fetches episode metadata using `-TvDbSeriesUrl` and optionally `-TvDbSeasonUrl`.
3. **Copy episodes** - Finds files under `-InputPath` matching `-FilePatterns` and larger than `-MinimumFileSize`, then copies them with TVDb-based naming.
4. **Extract chapters** - When `-ExtractChapters` is specified, extracts chapter sidecars for copied episodes.
5. **Extract captions** - Unless `-SkipCaptionExtraction` is specified, extracts English subtitles and optionally runs OCR.

Use `-Ocr` with values **Auto** (default), **Skip**, or **Force** to control image subtitle OCR after caption extraction. When OCR runs, OCR-produced SRT files are repaired by default; use `-SkipRepair` to skip repair. OCR parallelism is fixed at 10 concurrent conversions. When caption extraction runs, writes a SubtitleProcessingResult with extract/OCR counts.

Use `-EpisodeStart` when your source files begin mid-season (e.g. episode 5 on disc 1). `-MinimumFileSize` defaults to 1 GB but can be set to 0 to include all matching files.

The cmdlet fails with a terminating error if the TVDb scan returns no episodes or if no files are copied.

## EXAMPLES

### Example 1: Full season processing with chapter and caption extraction
```powershell
Invoke-SeriesProcessing -Title "My Show" -Season 1 -InputPath "C:\Source" -FilePatterns "*.mkv" -OutputPath "P:\TV" -TvDbSeriesUrl "https://thetvdb.com/series/12345" -ExtractChapters
```

Creates P:\TV\My Show\Season 01, scans TVDb, copies matching MKV files, extracts chapters, then extracts captions.

### Example 2: Skip caption extraction
```powershell
Invoke-SeriesProcessing -Title "Show" -Season 2 -InputPath "D:\Season2" -FilePatterns "*.mkv","*.mp4" -SkipCaptionExtraction
```

Copies episodes and optionally extracts chapters only; skips caption extraction.

### Example 3: Minimum file size and episode start
```powershell
Invoke-SeriesProcessing -Title "Show" -Season 1 -EpisodeStart 5 -InputPath "C:\Source" -FilePatterns "*.mkv" -MinimumFileSize 500MB
```

Processes files as episodes starting at episode 5; only files at least 500 MB are considered.

## PARAMETERS

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

### -ExtractChapters
When specified, extracts chapter files for copied episodes.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FilePatterns
File name patterns (wildcards) used to find episode files under InputPath.

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

### -InputPath
Root folder(s) containing source video files for the season.

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
Minimum file size in bytes required to treat a file as an episode. Default is 1 GB (1073741824 bytes). Set to 0 to include all matching files.

```yaml
Type: Int64
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 1073741824
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Root output directory.
When set, output is written to OutputPath\Title\Season XX.

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
Season number to process (1-based).

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

### -SkipCaptionExtraction
Skip caption extraction after copying episodes (chapters may still be extracted).

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Title
Series title used for TVDb lookup and for naming folders/files.

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
Optional TVDb series URL used as a starting point for season scans.

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

### -SkipRepair
Skip SRT repair during OCR processing. Has no effect when -Ocr is not specified.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -KeepSource
Keep source `.sup`/`.sub`/`.idx` files after a successful OCR conversion. Sources are deleted by default.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Ocr
Controls OCR of image-based captions after extraction. Default is Auto.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: Auto
Accept pipeline input: False
Accept wildcard characters: False
```

### -Alert
Play a system beep when the cmdlet finishes.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

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
Parameters are specified directly; InputPath and FilePatterns are required.

## OUTPUTS

### SubtitleProcessingResult
When caption extraction runs (unless `-SkipCaptionExtraction`): ExtractedCount, ConvertedCount, ExtractedPaths, and ConvertedPaths.

## NOTES
TVDb URLs are used to fetch episode metadata. If no episodes are returned or no files match, the cmdlet writes an error and stops.

## RELATED LINKS

[Invoke-SeasonScan](Invoke-SeasonScan.md)
[Invoke-VideoCopy](Invoke-VideoCopy.md)
[Split-SeriesChapters](Split-SeriesChapters.md)
