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
 [-TvDbSeasonUrl <String>] [-ExtractChapters] [-SkipCaptionExtraction] [-Ocr] [-SkipRepair]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Invoke-SeriesProcessing is a high-level workflow that: (1) creates a directory structure (OutputPath\Title\Season XX when -OutputPath is set), (2) scans TVDb for episode metadata (-TvDbSeriesUrl, -TvDbSeasonUrl), (3) copies video files from InputPath that match FilePatterns and exceed MinimumFileSize into the season folder with episode-based naming, (4) optionally extracts chapters (-ExtractChapters), and (5) optionally extracts captions (unless -SkipCaptionExtraction). When -Ocr is specified, extracted image captions are converted to SRT via OCR and repaired by default; use -SkipRepair to skip only the repair step. Use -EpisodeStart when your source files begin mid-season. The cmdlet fails if season scan returns no episodes or if no files are copied.

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

### -Ocr
Convert image captions to SRT via OCR and repair SRT files.

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

### None
This cmdlet does not write to the pipeline.

## NOTES
TVDb URLs are used to fetch episode metadata. If no episodes are returned or no files match, the cmdlet writes an error and stops.

## RELATED LINKS

[Invoke-SeasonScan](Invoke-SeasonScan.md)
[Invoke-VideoCopy](Invoke-VideoCopy.md)
[Split-SeriesChapters](Split-SeriesChapters.md)
