using Dadstart.Labs.MediaForge.Services.System;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class ExecutableResultTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange
        var output = "stdout";
        var errorOutput = "stderr";
        var exitCode = 0;
        var exception = new Exception("test");

        // Act
        var result = new ExecutableResult(output, errorOutput, exitCode, exception);

        // Assert
        Assert.Equal(output, result.Output);
        Assert.Equal(errorOutput, result.ErrorOutput);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal(exception, result.Exception);
    }

    [Fact]
    public void Constructor_WithoutException_SetsExceptionToNull()
    {
        // Arrange
        var output = "stdout";
        var errorOutput = "stderr";
        var exitCode = 0;

        // Act
        var result = new ExecutableResult(output, errorOutput, exitCode);

        // Assert
        Assert.Equal(output, result.Output);
        Assert.Equal(errorOutput, result.ErrorOutput);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Constructor_WithNullValues_AllowsNulls()
    {
        // Arrange & Act
        var result = new ExecutableResult(null, null, null);

        // Assert
        Assert.Null(result.Output);
        Assert.Null(result.ErrorOutput);
        Assert.Null(result.ExitCode);
        Assert.Null(result.Exception);
    }
}
