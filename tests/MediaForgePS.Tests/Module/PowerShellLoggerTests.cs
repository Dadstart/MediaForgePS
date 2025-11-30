using Dadstart.Labs.MediaForge.Module;
using System.Management.Automation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Module;

public class PowerShellLoggerTests
{
    [Fact]
    public void Constructor_WithCategory_SetsCategory()
    {
        // Arrange
        var category = "TestCategory";

        // Act
        var logger = new PowerShellLogger(category);

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void IsEnabled_Always_ReturnsTrue()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");

        // Act & Assert
        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
        Assert.True(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public void BeginScope_ReturnsNullDisposable()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");

        // Act
        var scope = logger.BeginScope("TestState");

        // Assert
        Assert.NotNull(scope);
        scope.Dispose(); // Should not throw
    }

    [Fact]
    public void Log_WithNullFormatter_DoesNotThrow()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        CmdletContext.Current = null; // No cmdlet context

        // Act & Assert
        logger.Log(LogLevel.Information, new EventId(1), "state", null, null!);
    }

    [Fact]
    public void Log_WithNullCmdletContext_DoesNotThrow()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        CmdletContext.Current = null;

        // Act & Assert
        logger.Log(LogLevel.Information, new EventId(1), "state", null, (s, e) => "message");
    }

    [Fact]
    public void Log_Trace_WithCmdletContext_CallsWriteVerbose()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log(LogLevel.Trace, new EventId(1), "state", null, (s, e) => "trace message");

        // Assert
        mockCmdlet.Verify(c => c.WriteVerbose(It.Is<string>(msg => msg.Contains("trace message"))), Times.Once);
    }

    [Fact]
    public void Log_Debug_WithCmdletContext_CallsWriteDebug()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log(LogLevel.Debug, new EventId(1), "state", null, (s, e) => "debug message");

        // Assert
        mockCmdlet.Verify(c => c.WriteDebug(It.Is<string>(msg => msg.Contains("debug message"))), Times.Once);
    }

    [Fact]
    public void Log_Information_WithCmdletContext_CallsWriteInformation()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log(LogLevel.Information, new EventId(1), "state", null, (s, e) => "info message");

        // Assert
        mockCmdlet.Verify(c => c.WriteInformation(It.Is<InformationRecord>(r => r.MessageData.ToString()!.Contains("info message"))), Times.Once);
    }

    [Fact]
    public void Log_Warning_WithCmdletContext_CallsWriteWarning()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log(LogLevel.Warning, new EventId(1), "state", null, (s, e) => "warning message");

        // Assert
        mockCmdlet.Verify(c => c.WriteWarning(It.Is<string>(msg => msg.Contains("warning message"))), Times.Once);
    }

    [Fact]
    public void Log_Error_WithCmdletContext_CallsWriteError()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;
        var exception = new Exception("test exception");

        // Act
        logger.Log(LogLevel.Error, new EventId(1), "state", exception, (s, e) => "error message");

        // Assert
        mockCmdlet.Verify(c => c.WriteError(It.Is<ErrorRecord>(r => r.Exception == exception)), Times.Once);
    }

    [Fact]
    public void Log_Critical_WithCmdletContext_CallsWriteError()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;
        var exception = new Exception("critical exception");

        // Act
        logger.Log(LogLevel.Critical, new EventId(1), "state", exception, (s, e) => "critical message");

        // Assert
        mockCmdlet.Verify(c => c.WriteError(It.Is<ErrorRecord>(r => r.Exception == exception)), Times.Once);
    }

    [Fact]
    public void Log_Error_WithoutException_CreatesException()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log(LogLevel.Error, new EventId(1), "state", null, (s, e) => "error message");

        // Assert
        mockCmdlet.Verify(c => c.WriteError(It.Is<ErrorRecord>(r => r.Exception != null)), Times.Once);
    }

    [Fact]
    public void Log_WithCategory_PrependsCategory()
    {
        // Arrange
        var category = "MyCategory";
        var logger = new PowerShellLogger(category);
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log(LogLevel.Information, new EventId(1), "state", null, (s, e) => "message");

        // Assert
        mockCmdlet.Verify(c => c.WriteInformation(It.Is<InformationRecord>(r => r.MessageData.ToString()!.Contains($"[{category}]"))), Times.Once);
    }

    [Fact]
    public void Log_WithEmptyCategory_DoesNotPrependCategory()
    {
        // Arrange
        var logger = new PowerShellLogger(string.Empty);
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log(LogLevel.Information, new EventId(1), "state", null, (s, e) => "message");

        // Assert
        mockCmdlet.Verify(c => c.WriteInformation(It.Is<InformationRecord>(r => !r.MessageData.ToString()!.StartsWith("["))), Times.Once);
    }

    [Fact]
    public void Log_WithEmptyMessageAndNoException_DoesNotCallCmdlet()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log(LogLevel.Information, new EventId(1), "state", null, (s, e) => string.Empty);

        // Assert
        mockCmdlet.Verify(c => c.WriteInformation(It.IsAny<InformationRecord>()), Times.Never);
    }

    [Fact]
    public void Log_WithExceptionButEmptyMessage_CallsCmdlet()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;
        var exception = new Exception("test");

        // Act
        logger.Log(LogLevel.Error, new EventId(1), "state", exception, (s, e) => string.Empty);

        // Assert
        mockCmdlet.Verify(c => c.WriteError(It.IsAny<ErrorRecord>()), Times.Once);
    }

    [Fact]
    public void Log_DefaultCase_CallsWriteVerbose()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act
        logger.Log((LogLevel)999, new EventId(1), "state", null, (s, e) => "message");

        // Assert
        mockCmdlet.Verify(c => c.WriteVerbose(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Log_WhenCmdletThrows_DoesNotPropagateException()
    {
        // Arrange
        var logger = new PowerShellLogger("TestCategory");
        var mockCmdlet = new Mock<PSCmdlet>();
        mockCmdlet.Setup(c => c.WriteVerbose(It.IsAny<string>())).Throws<Exception>();
        CmdletContext.Current = mockCmdlet.Object;

        // Act & Assert
        logger.Log(LogLevel.Trace, new EventId(1), "state", null, (s, e) => "message");
        // Should not throw
    }
}
