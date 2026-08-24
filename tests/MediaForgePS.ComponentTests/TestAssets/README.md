MediaForgePS Component Test Assets
==================================

Tiny media files used by `MediaForgePS.ComponentTests`. They are copied to test output via project `Content` items and are checked into the repository so CI and local runs behave the same.

Assets
------

| File | Purpose |
|------|---------|
| `sample-1s.mkv` | Valid ~1 s video with a single audio track — used for happy-path cmdlet tests |
| `invalid-media.mkv` | Small text file with an `.mkv` extension — exercises ffprobe error paths |
| `ocr-broken.srt` | SRT with common OCR misreads (J/♪, pipe/I, [$10]) — used by Repair-Subtitles component tests |

Image subtitle OCR component tests generate ephemeral VobSub `.sub`/`.idx` fixtures at runtime (Windows + Tesseract `eng.traineddata`). They skip when tessdata is missing unless `MEDIAFORGE_REQUIRE_COMPONENT_TESTS=1`.

Requirements
------------

Component tests require `ffmpeg` and `ffprobe` on `PATH`. If either tool or these assets is missing, tests skip via `SkipException` locally.

In CI, set `MEDIAFORGE_REQUIRE_COMPONENT_TESTS=1` so missing tools or assets fail the run instead of skipping. The GitHub Actions workflow installs ffmpeg on Linux, macOS, and Windows, installs Tesseract `eng.traineddata` on Windows (for OCR component tests), and sets that variable.

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
