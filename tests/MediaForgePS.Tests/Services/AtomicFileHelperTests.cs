using System;
using System.IO;
using System.Text;
using Dadstart.Labs.MediaForge.Services;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services;

public class AtomicFileHelperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_Atomic_" + Guid.NewGuid().ToString("N"));

    public AtomicFileHelperTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void CreateTempSiblingPath_UsesSameDirectoryWithTmpSuffix()
    {
        var finalPath = Path.Combine(_tempDir, "output.mp4");

        var tempPath = AtomicFileHelper.CreateTempSiblingPath(finalPath);

        Assert.Equal(Path.GetDirectoryName(finalPath), Path.GetDirectoryName(tempPath));
        Assert.Contains(".mediaforge.tmp.", Path.GetFileName(tempPath), StringComparison.Ordinal);
        Assert.EndsWith(".mp4", tempPath, StringComparison.Ordinal);
        Assert.StartsWith("output.mediaforge.tmp.", Path.GetFileName(tempPath), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateTempDirectory_CreatesUniqueDirectoryUnderSystemTemp()
    {
        var path = AtomicFileHelper.CreateTempDirectory();
        try
        {
            Assert.True(Directory.Exists(path));
            Assert.Equal(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetDirectoryName(path));
            Assert.StartsWith("MediaForgePS_", Path.GetFileName(path), StringComparison.Ordinal);
        }
        finally
        {
            AtomicFileHelper.TryDeleteDirectory(path);
        }
    }

    [Fact]
    public void CreateTempOutputPath_PreservesFileNameUnderSystemTempDirectory()
    {
        var finalPath = Path.Combine(_tempDir, "final cut.mp4");

        var tempPath = AtomicFileHelper.CreateTempOutputPath(finalPath);
        var tempDirectory = Path.GetDirectoryName(tempPath);
        try
        {
            Assert.Equal("final cut.mp4", Path.GetFileName(tempPath));
            Assert.NotEqual(Path.GetDirectoryName(finalPath), tempDirectory);
            Assert.StartsWith(
                Path.Combine(Path.GetTempPath(), "MediaForgePS_"),
                tempDirectory,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(tempDirectory));
        }
        finally
        {
            AtomicFileHelper.TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void TryDeleteDirectory_RemovesDirectoryAndContents()
    {
        var directory = AtomicFileHelper.CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "staging.mp4"), "data");

        AtomicFileHelper.TryDeleteDirectory(directory);

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void WriteTextAtomically_CreatesFinalFileWithoutLeavingTemp()
    {
        var finalPath = Path.Combine(_tempDir, "file.srt");

        AtomicFileHelper.WriteTextAtomically(finalPath, "hello", Encoding.UTF8);

        Assert.True(File.Exists(finalPath));
        Assert.Equal("hello", File.ReadAllText(finalPath));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.mediaforge.tmp.*"));
    }

    [Fact]
    public void WriteTextAtomically_ReplacesExistingContent()
    {
        var finalPath = Path.Combine(_tempDir, "file.srt");
        File.WriteAllText(finalPath, "old");

        AtomicFileHelper.WriteTextAtomically(finalPath, "new", Encoding.UTF8, overwrite: true);

        Assert.Equal("new", File.ReadAllText(finalPath));
    }

    [Fact]
    public void PromoteTempFile_MovesTempOntoFinal()
    {
        var finalPath = Path.Combine(_tempDir, "out.mp4");
        var tempPath = AtomicFileHelper.CreateTempSiblingPath(finalPath);
        File.WriteAllText(tempPath, "encoded");

        AtomicFileHelper.PromoteTempFile(tempPath, finalPath);

        Assert.True(File.Exists(finalPath));
        Assert.False(File.Exists(tempPath));
        Assert.Equal("encoded", File.ReadAllText(finalPath));
    }

    [Fact]
    public void PromoteTempFile_WhenDestinationExistsWithoutOverwrite_Throws()
    {
        var finalPath = Path.Combine(_tempDir, "exists.mp4");
        File.WriteAllText(finalPath, "original");
        var tempPath = AtomicFileHelper.CreateTempSiblingPath(finalPath);
        File.WriteAllText(tempPath, "encoded");

        var ex = Assert.Throws<IOException>(() => AtomicFileHelper.PromoteTempFile(tempPath, finalPath));
        Assert.Contains("-Force", ex.Message, StringComparison.Ordinal);
        Assert.Equal("original", File.ReadAllText(finalPath));
        Assert.True(File.Exists(tempPath));
    }

    [Fact]
    public void PromoteTempFile_WhenDestinationExistsWithOverwrite_ReplacesFile()
    {
        var finalPath = Path.Combine(_tempDir, "replace.mp4");
        File.WriteAllText(finalPath, "original");
        var tempPath = AtomicFileHelper.CreateTempSiblingPath(finalPath);
        File.WriteAllText(tempPath, "encoded");

        AtomicFileHelper.PromoteTempFile(tempPath, finalPath, overwrite: true);

        Assert.True(File.Exists(finalPath));
        Assert.False(File.Exists(tempPath));
        Assert.Equal("encoded", File.ReadAllText(finalPath));
    }

    [Theory]
    [InlineData("NUL")]
    [InlineData("nul")]
    [InlineData("/dev/null")]
    public void IsNullMuxerOutput_RecognizesPlatformNullDevices(string path)
    {
        Assert.True(AtomicFileHelper.IsNullMuxerOutput(path));
    }

    [Fact]
    public void IsNullMuxerOutput_RejectsOrdinaryPaths()
    {
        Assert.False(AtomicFileHelper.IsNullMuxerOutput(Path.Combine(_tempDir, "out.mp4")));
    }
}
