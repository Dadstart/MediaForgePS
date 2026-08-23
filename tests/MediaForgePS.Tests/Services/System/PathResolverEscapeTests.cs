using System;
using System.IO;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class PathResolverEscapeTests
{
    [Theory]
    [InlineData(@"C:\Movies\Hall Pass (2011) [DVD].mkv")]
    [InlineData(@"C:\Movies\Hall Pass (2011) `[DVD`].mkv")]
    [InlineData(@"C:\Movies\Hall Pass (2011) ```[DVD```].mkv")]
    public void EscapeLiteralProviderPath_WithBracketVariants_IsIdempotentAndLiteral(string path)
    {
        var escaped = PathResolver.EscapeLiteralProviderPath(path);
        var escapedAgain = PathResolver.EscapeLiteralProviderPath(escaped);

        Assert.Equal(@"C:\Movies\Hall Pass (2011) `[DVD`].mkv", escaped);
        Assert.Equal(escaped, escapedAgain);
        Assert.Equal(WildcardPattern.Escape(@"C:\Movies\Hall Pass (2011) [DVD].mkv"), escaped);
    }

    [Theory]
    [InlineData(@"C:\clips\show`[1`].mkv", @"C:\clips\show`[1`].mkv")]
    [InlineData(@"C:\clips\file`*.mkv", @"C:\clips\file`*.mkv")]
    [InlineData(@"C:\clips\file`?.mkv", @"C:\clips\file`?.mkv")]
    [InlineData(@"C:\clips\plain.mkv", @"C:\clips\plain.mkv")]
    public void EscapeLiteralProviderPath_WithOtherWildcardEscapes_NormalizesThenEscapes(string input, string expected)
    {
        Assert.Equal(expected, PathResolver.EscapeLiteralProviderPath(input));
    }

    [Fact]
    public void EscapeLiteralProviderPath_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PathResolver.EscapeLiteralProviderPath(null!));
    }
}

public class PathResolverEnsureOutputDirectoryTests
{
    [Fact]
    public void EnsureOutputDirectoryExists_WhenParentMissing_CreatesDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS_PathResolver_" + Guid.NewGuid().ToString("N"));
        var nestedDir = Path.Combine(root, "nested", "out");
        var filePath = Path.Combine(nestedDir, "file.mp4");
        try
        {
            Assert.False(Directory.Exists(nestedDir));

            var resolver = new PathResolver(NullLogger<PathResolver>.Instance);
            resolver.EnsureOutputDirectoryExists(filePath);

            Assert.True(Directory.Exists(nestedDir));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureOutputDirectoryExists_WhenParentExists_DoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaForgePS_PathResolver_" + Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "file.mp4");
        try
        {
            Directory.CreateDirectory(root);

            var resolver = new PathResolver(NullLogger<PathResolver>.Instance);
            resolver.EnsureOutputDirectoryExists(filePath);

            Assert.True(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureOutputDirectoryExists_WhenPathNullOrWhitespace_Throws()
    {
        var resolver = new PathResolver(NullLogger<PathResolver>.Instance);
        Assert.Throws<ArgumentException>(() => resolver.EnsureOutputDirectoryExists(" "));
    }
}
