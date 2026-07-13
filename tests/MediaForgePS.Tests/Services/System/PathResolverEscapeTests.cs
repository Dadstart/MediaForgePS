using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services.System;
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
