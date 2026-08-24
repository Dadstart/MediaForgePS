using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class SrtRepairHelperTests
{
    [Fact]
    public void CopyToBackupWithRelativePath_WhenCopySucceeds_WritesVerboseAndReturnsTrue()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.CreateFile("source.srt", "1\n00:00:01,000 --> 00:00:02,000\nLine\n");
        var io = new FakeCmdletIO();

        var copied = SrtRepairHelper.CopyToBackupWithRelativePath(
            io,
            NullLogger.Instance,
            temp.BackupRoot,
            sourcePath,
            "backup/source.srt");

        Assert.True(copied);
        Assert.Contains("Backed up to:", Assert.Single(io.VerboseMessages), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(temp.BackupRoot, "backup", "source.srt")));
    }

    [Fact]
    public void CopyToBackupWithRelativePath_WhenCopyFails_WritesErrorAndReturnsFalse()
    {
        var io = new FakeCmdletIO();

        var copied = SrtRepairHelper.CopyToBackupWithRelativePath(
            io,
            NullLogger.Instance,
            backupRoot: @"C:\nonexistent\backup\root",
            sourceFilePath: @"C:\also\missing.srt",
            relativePath: "missing.srt");

        Assert.False(copied);
        var error = Assert.Single(io.Errors);
        Assert.Equal("BackupFailed", error.FullyQualifiedErrorId);
        Assert.Equal(ErrorCategory.WriteError, error.CategoryInfo.Category);
    }

    [Fact]
    public void CopyToBackupFromPathRoot_UsesRelativePathFromDriveRoot()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.CreateFile("source.srt", "content");
        var io = new FakeCmdletIO();
        var fullPath = Path.GetFullPath(sourcePath);
        var pathRoot = Path.GetPathRoot(fullPath)!;
        var expectedRelative = Path.GetRelativePath(pathRoot, fullPath)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var copied = SrtRepairHelper.CopyToBackupFromPathRoot(
            io,
            NullLogger.Instance,
            temp.BackupRoot,
            sourcePath);

        Assert.True(copied);
        Assert.True(File.Exists(Path.Combine(temp.BackupRoot, expectedRelative)));
    }

    [Fact]
    public void RepairFileWithReporting_WhenRepairSucceeds_WritesVerboseAndReturnsTrue()
    {
        using var temp = new TempDirectory();
        var inputPath = temp.CreateFile("broken.srt", "1\n00:00:01,000 --> 00:00:02,000\nSong J plays.\n");
        var outputPath = temp.GetPath("repaired.srt");
        var io = new FakeCmdletIO();

        var repaired = SrtRepairHelper.RepairFileWithReporting(io, NullLogger.Instance, inputPath, outputPath);

        Assert.True(repaired);
        Assert.Contains("Repaired:", Assert.Single(io.VerboseMessages), StringComparison.Ordinal);
        Assert.Contains("Song ♪ plays.", File.ReadAllText(outputPath));
    }

    [Fact]
    public void RepairFileWithReporting_WhenRepairFails_WritesErrorAndReturnsFalse()
    {
        var io = new FakeCmdletIO();

        var repaired = SrtRepairHelper.RepairFileWithReporting(
            io,
            NullLogger.Instance,
            inputPath: @"C:\missing\broken.srt",
            outputPath: @"C:\missing\repaired.srt");

        Assert.False(repaired);
        var error = Assert.Single(io.Errors);
        Assert.Equal("RepairSubtitlesFailed", error.FullyQualifiedErrorId);
        Assert.Equal(ErrorCategory.WriteError, error.CategoryInfo.Category);
    }

    [Fact]
    public void RunRepairLoop_WhenRepairItemsEmpty_DoesNothing()
    {
        var io = new FakeCmdletIO();
        var pathResolverMock = new Mock<IPathResolver>();

        SrtRepairHelper.RunRepairLoop(
            io,
            NullLogger.Instance,
            pathResolverMock.Object,
            Array.Empty<SrtRepairHelper.SrtRepairItem>(),
            shouldRepair: true,
            backupPath: null);

        Assert.Empty(io.ProgressRecords);
        Assert.Empty(io.VerboseMessages);
        pathResolverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RunRepairLoop_WhenShouldRepairFalse_SkipsBackupAndRepair()
    {
        using var temp = new TempDirectory();
        var srtPath = temp.CreateFile("broken.srt", "1\n00:00:01,000 --> 00:00:02,000\nSong J plays.\n");
        var io = new FakeCmdletIO();
        var pathResolverMock = new Mock<IPathResolver>();

        SrtRepairHelper.RunRepairLoop(
            io,
            NullLogger.Instance,
            pathResolverMock.Object,
            [srtPath],
            shouldRepair: false,
            backupPath: null);

        Assert.Contains(io.ProgressRecords, record => record.Activity.Contains("Repairing subtitles", StringComparison.Ordinal));
        Assert.DoesNotContain("Song ♪ plays.", File.ReadAllText(srtPath));
        Assert.Empty(io.VerboseMessages);
        pathResolverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void RunRepairLoop_WhenBackupPathResolutionFails_WritesErrorAndSkipsRepair()
    {
        using var temp = new TempDirectory();
        var srtPath = temp.CreateFile("broken.srt", "1\n00:00:01,000 --> 00:00:02,000\nSong J plays.\n");
        var io = new FakeCmdletIO();
        var pathResolverMock = new Mock<IPathResolver>();
        pathResolverMock.Setup(r => r.TryResolveOutputPath("bad-backup", out It.Ref<string?>.IsAny))
            .Returns(false);

        SrtRepairHelper.RunRepairLoop(
            io,
            NullLogger.Instance,
            pathResolverMock.Object,
            [srtPath],
            shouldRepair: true,
            backupPath: "bad-backup");

        var error = Assert.Single(io.Errors);
        Assert.Equal("BackupPathResolutionFailed", error.FullyQualifiedErrorId);
        Assert.DoesNotContain("Song ♪ plays.", File.ReadAllText(srtPath));
        Assert.DoesNotContain(io.VerboseMessages, message => message.StartsWith("Repaired:", StringComparison.Ordinal));
    }

    [Fact]
    public void RunRepairLoop_WhenRepairItemsProvided_BacksUpAndRepairsInPlace()
    {
        using var temp = new TempDirectory();
        var srtPath = temp.CreateFile("broken.srt", "1\n00:00:01,000 --> 00:00:02,000\nSong J plays.\n");
        var io = new FakeCmdletIO();
        var pathResolverMock = new Mock<IPathResolver>();
        string resolvedBackup = temp.BackupRoot;
        pathResolverMock.Setup(r => r.TryResolveOutputPath(temp.BackupRoot, out resolvedBackup))
            .Returns(true);

        SrtRepairHelper.RunRepairLoop(
            io,
            NullLogger.Instance,
            pathResolverMock.Object,
            [new SrtRepairHelper.SrtRepairItem(srtPath, srtPath, "broken.srt")],
            shouldRepair: true,
            backupPath: temp.BackupRoot);

        Assert.Contains("Song ♪ plays.", File.ReadAllText(srtPath));
        Assert.True(File.Exists(Path.Combine(temp.BackupRoot, "broken.srt")));
        Assert.Contains(io.VerboseMessages, message => message.StartsWith("Backed up to:", StringComparison.Ordinal));
        Assert.Contains(io.VerboseMessages, message => message.StartsWith("Repaired:", StringComparison.Ordinal));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "MediaForgePS_SrtRepair_" + Guid.NewGuid().ToString("N"));
            BackupRoot = Path.Combine(Root, "backup");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(BackupRoot);
        }

        public string Root { get; }

        public string BackupRoot { get; }

        public string CreateFile(string fileName, string content)
        {
            var filePath = Path.Combine(Root, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        public string GetPath(string fileName) => Path.Combine(Root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
