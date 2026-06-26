MediaForgePS Component Test Assets
==================================

Tiny media files used by `MediaForgePS.ComponentTests`. They are copied to test output via project `Content` items and are checked into the repository so CI and local runs behave the same.

Assets
------

| File | Purpose |
|------|---------|
| `sample-1s.mkv` | Valid ~1 s video with a single audio track — used for happy-path cmdlet tests |
| `invalid-media.mkv` | Small text file with an `.mkv` extension — exercises ffprobe error paths |

Requirements
------------

Component tests require `ffmpeg` and `ffprobe` on `PATH`. If either tool or these assets is missing, tests skip via `[SkippableFact]`.

Regenerating assets
-------------------

From the repository root (requires `ffmpeg` on `PATH`):

Create the sample video:

```powershell
ffmpeg -f lavfi -i testsrc=size=320x240:rate=25 -t 1 -pix_fmt yuv420p tests/MediaForgePS.ComponentTests/TestAssets/sample-1s.mkv
```

Create the invalid media file:

```powershell
'not a real media file' | Set-Content -NoNewline tests/MediaForgePS.ComponentTests/TestAssets/invalid-media.mkv
```

Commit regenerated files so they stay available on all machines and CI agents.
