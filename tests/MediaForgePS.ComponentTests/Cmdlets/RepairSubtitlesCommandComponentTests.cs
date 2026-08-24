using System;
using System.IO;
using System.Linq;
using Dadstart.Labs.MediaForge.Cmdlets;
using Xunit;

namespace Dadstart.Labs.MediaForge.ComponentTests.Cmdlets;

public class RepairSubtitlesCommandComponentTests : ComponentTestBase
{
    [Fact]
    public void RepairSubtitles_WithSampleOcrBrokenSrt_RepairsInPlace()
    {
        SkipIfOcrBrokenSrtAssetMissing();

        var srtPath = CopyOcrBrokenSrtAs("repair-in-place.srt");

        using var ps = CreatePowerShellFor<RepairSubtitlesCommand>("Repair-Subtitles");
        ps.AddCommand("Repair-Subtitles")
            .AddParameter("InputPath", srtPath)
            .AddParameter("Confirm", false);

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        var repaired = File.ReadAllText(srtPath);
        Assert.Contains("Song ♪ plays.", repaired);
        Assert.Contains("I think I am right.", repaired);
        Assert.Contains("[♪♪♪] Lyric line ♪", repaired);
    }

    [Fact]
    public void RepairSubtitles_WithOutputPath_WritesRepairedCopyWithoutModifyingSource()
    {
        SkipIfOcrBrokenSrtAssetMissing();

        var inputPath = CopyOcrBrokenSrtAs("repair-input.srt");
        var outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, "repair-output.srt");
        var original = File.ReadAllText(inputPath);

        using var ps = CreatePowerShellFor<RepairSubtitlesCommand>("Repair-Subtitles");
        ps.AddCommand("Repair-Subtitles")
            .AddParameter("InputPath", inputPath)
            .AddParameter("OutputPath", outputPath)
            .AddParameter("Confirm", false);

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Equal(original, File.ReadAllText(inputPath));
        Assert.True(File.Exists(outputPath));
        Assert.Contains("Song ♪ plays.", File.ReadAllText(outputPath));
    }

    [Fact]
    public void RepairSubtitles_WithDirectoryInput_RepairsAllSrtFiles()
    {
        SkipIfOcrBrokenSrtAssetMissing();

        var directory = CreateTempDirectory();
        var firstPath = Path.Combine(directory, "first.srt");
        var secondPath = Path.Combine(directory, "second.srt");
        File.Copy(OcrBrokenSrtPath, firstPath);
        File.Copy(OcrBrokenSrtPath, secondPath);

        using var ps = CreatePowerShellFor<RepairSubtitlesCommand>("Repair-Subtitles");
        ps.AddCommand("Repair-Subtitles")
            .AddParameter("InputPath", directory)
            .AddParameter("Confirm", false);

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Contains("Song ♪ plays.", File.ReadAllText(firstPath));
        Assert.Contains("Song ♪ plays.", File.ReadAllText(secondPath));
    }

    [Fact]
    public void RepairSubtitles_WithBackupPath_PreservesOriginalUnderBackupRoot()
    {
        SkipIfOcrBrokenSrtAssetMissing();

        var srtPath = CopyOcrBrokenSrtAs("repair-backup.srt");
        var backupRoot = CreateTempDirectory();
        var original = File.ReadAllText(srtPath);

        using var ps = CreatePowerShellFor<RepairSubtitlesCommand>("Repair-Subtitles");
        ps.AddCommand("Repair-Subtitles")
            .AddParameter("InputPath", srtPath)
            .AddParameter("BackupPath", backupRoot)
            .AddParameter("Confirm", false);

        ps.Invoke();
        var errors = ps.Streams.Error.ReadAll();

        Assert.Empty(errors);
        Assert.Contains("Song ♪ plays.", File.ReadAllText(srtPath));
        var backupFiles = Directory.EnumerateFiles(backupRoot, "*.srt", SearchOption.AllDirectories).ToList();
        Assert.Single(backupFiles);
        Assert.Equal(original, File.ReadAllText(backupFiles[0]));
    }

    private void SkipIfOcrBrokenSrtAssetMissing()
    {
        if (File.Exists(OcrBrokenSrtPath))
            return;

        FailOrSkip("Component test SRT asset is missing. Add ocr-broken.srt under TestAssets.");
    }
}
