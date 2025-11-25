using Dadstart.Labs.MediaForge.Services.System;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class ProcessArgumentExtensionsTests
{
    [Fact]
    public void ToQuotedArgumentString_WithEmptyCollection_ReturnsEmptyString()
    {
        // Arrange
        var platformService = new Mock<IPlatformService>();
        platformService.Setup(p => p.IsWindows()).Returns(true);
        var arguments = Array.Empty<string>();

        // Act
        var result = arguments.ToQuotedArgumentString(platformService.Object);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ToQuotedArgumentString_WithSingleArgument_ReturnsQuotedArgument()
    {
        // Arrange
        var platformService = new Mock<IPlatformService>();
        platformService.Setup(p => p.IsWindows()).Returns(true);
        var arguments = new[] { "test" };

        // Act
        var result = arguments.ToQuotedArgumentString(platformService.Object);

        // Assert
        Assert.Equal("test", result);
    }

    [Fact]
    public void ToQuotedArgumentString_WithMultipleArguments_ReturnsSpaceSeparatedQuotedArguments()
    {
        // Arrange
        var platformService = new Mock<IPlatformService>();
        platformService.Setup(p => p.IsWindows()).Returns(true);
        var arguments = new[] { "arg1", "arg2", "arg3" };

        // Act
        var result = arguments.ToQuotedArgumentString(platformService.Object);

        // Assert
        Assert.Equal("arg1 arg2 arg3", result);
    }

    [Fact]
    public void ToQuotedArgumentString_WithArgumentsContainingSpaces_QuotesEachArgument()
    {
        // Arrange
        var platformService = new Mock<IPlatformService>();
        platformService.Setup(p => p.IsWindows()).Returns(true);
        var arguments = new[] { "arg with spaces", "another arg" };

        // Act
        var result = arguments.ToQuotedArgumentString(platformService.Object);

        // Assert
        Assert.Equal("\"arg with spaces\" \"another arg\"", result);
    }

    [Fact]
    public void QuoteArgument_WithWindowsPlatform_ReturnsWindowsQuotedArgument()
    {
        // Arrange
        var platformService = new Mock<IPlatformService>();
        platformService.Setup(p => p.IsWindows()).Returns(true);

        // Act
        var result = ProcessArgumentExtensions.QuoteArgument("test with spaces", platformService.Object);

        // Assert
        Assert.Equal("\"test with spaces\"", result);
    }

    [Fact]
    public void QuoteArgument_WithUnixPlatform_ReturnsUnixQuotedArgument()
    {
        // Arrange
        var platformService = new Mock<IPlatformService>();
        platformService.Setup(p => p.IsWindows()).Returns(false);

        // Act
        var result = ProcessArgumentExtensions.QuoteArgument("test with spaces", platformService.Object);

        // Assert
        Assert.Equal("'test with spaces'", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithNull_ReturnsEmptyQuotes()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument(null!);

        // Assert
        Assert.Equal("\"\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithEmptyString_ReturnsEmptyQuotes()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument(string.Empty);

        // Assert
        Assert.Equal("\"\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithSimpleArgument_ReturnsUnquoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("simple");

        // Assert
        Assert.Equal("simple", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithArgumentContainingSpace_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("arg with space");

        // Assert
        Assert.Equal("\"arg with space\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithArgumentContainingTab_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("arg\twith\ttab");

        // Assert
        Assert.Equal("\"arg\twith\ttab\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithArgumentContainingQuote_EscapesQuote()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("arg\"with\"quote");

        // Assert
        Assert.Equal("\"arg\\\"with\\\"quote\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithArgumentContainingBackslash_EscapesCorrectly()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("C:\\path\\to\\file");

        // Assert
        Assert.Equal("\"C:\\path\\to\\file\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithBackslashBeforeQuote_DoublesBackslashes()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("path\\\"to");

        // Assert
        Assert.Equal("\"path\\\\\\\"to\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithTrailingBackslash_DoublesTrailingBackslashes()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("path\\");

        // Assert
        Assert.Equal("\"path\\\\\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithMultipleBackslashesBeforeQuote_HandlesCorrectly()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("path\\\\\"to");

        // Assert
        Assert.Equal("\"path\\\\\\\\\\\"to\"", result);
    }

    [Fact]
    public void QuoteWindowsArgument_WithComplexPath_QuotesCorrectly()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteWindowsArgument("C:\\Program Files\\My App\\file.txt");

        // Assert
        Assert.Equal("\"C:\\Program Files\\My App\\file.txt\"", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithNull_ReturnsEmptyQuotes()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument(null!);

        // Assert
        Assert.Equal("''", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithEmptyString_ReturnsEmptyQuotes()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument(string.Empty);

        // Assert
        Assert.Equal("''", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithSimpleArgument_ReturnsUnquoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("simple");

        // Assert
        Assert.Equal("simple", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingSpace_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("arg with space");

        // Assert
        Assert.Equal("'arg with space'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingTab_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("arg\twith\ttab");

        // Assert
        Assert.Equal("'arg\twith\ttab'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingSingleQuote_EscapesCorrectly()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("don't");

        // Assert
        Assert.Equal("'don'\\''t'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingDoubleQuote_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("arg\"with\"quote");

        // Assert
        Assert.Equal("'arg\"with\"quote'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingBackslash_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("path\\to\\file");

        // Assert
        Assert.Equal("'path\\to\\file'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingDollarSign_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("var$value");

        // Assert
        Assert.Equal("'var$value'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingBacktick_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("command`sub");

        // Assert
        Assert.Equal("'command`sub'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingWildcard_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("file*.txt");

        // Assert
        Assert.Equal("'file*.txt'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingPipe_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("cmd1|cmd2");

        // Assert
        Assert.Equal("'cmd1|cmd2'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingAmpersand_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("cmd1&cmd2");

        // Assert
        Assert.Equal("'cmd1&cmd2'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingSemicolon_ReturnsQuoted()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("cmd1;cmd2");

        // Assert
        Assert.Equal("'cmd1;cmd2'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithArgumentContainingMultipleSingleQuotes_EscapesAll()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("don't can't won't");

        // Assert
        Assert.Equal("'don'\\''t can'\\''t won'\\''t'", result);
    }

    [Fact]
    public void QuoteUnixArgument_WithComplexPath_QuotesCorrectly()
    {
        // Act
        var result = ProcessArgumentExtensions.QuoteUnixArgument("/home/user/my files/file.txt");

        // Assert
        Assert.Equal("'/home/user/my files/file.txt'", result);
    }
}

