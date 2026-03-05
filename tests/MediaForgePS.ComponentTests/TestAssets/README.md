MediaForgePS Component Test Assets
==================================

This directory is intended to hold tiny media files used by the
`MediaForgePS.ComponentTests` project. These assets are required
for the ffmpeg/ffprobe-backed component tests to run as true
component tests instead of being skipped.

Recommended assets
------------------

- A very small valid video file with a single audio track, for
  example `sample-1s.mkv`, roughly one second long at a low
  resolution.
- An intentionally invalid media file, for example
  `invalid-media.mkv`, which is just a small text file. This is
  used to exercise error paths when probing media metadata.

Example commands to generate assets (from the repo root)
-------------------------------------------------------

The commands below assume `ffmpeg` is installed and available on
your PATH.

Create a tiny sample video:

```bash
ffmpeg -f lavfi -i testsrc=size=320x240:rate=25 -t 1 -pix_fmt yuv420p tests/MediaForgePS.ComponentTests/TestAssets/sample-1s.mkv
```

Create an invalid “media” file:

```bash
echo "not a real media file" > tests/MediaForgePS.ComponentTests/TestAssets/invalid-media.mkv
```

After generating these files, add them to source control so they
are available on all machines and CI agents that run the component
tests.

