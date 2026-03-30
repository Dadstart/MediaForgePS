using System.Linq;
using Dadstart.Labs.MediaForge.Services.System;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class ArgumentBuilderTests
{
    [Fact]
    public void ToArguments_WithValuesContainingSpaces_ReturnsRawValues()
    {
        var builder = new ArgumentBuilder();

        var args = builder
            .AddOption("-i", @"C:\Program Files\input.mkv")
            .AddOption("-metadata", "title=My Track")
            .ToArguments()
            .ToArray();

        Assert.Equal(["-i", @"C:\Program Files\input.mkv", "-metadata", "title=My Track"], args);
    }

    [Fact]
    public void AddOptionIfNotNull_WithWhitespaceValue_DoesNotAddArgument()
    {
        var builder = new ArgumentBuilder();

        var args = builder
            .AddFlag("-y")
            .AddOptionIfNotNull("-x265-params", "   ")
            .ToArguments()
            .ToArray();

        Assert.Equal(["-y"], args);
    }
}
