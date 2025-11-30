using System;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using Moq;
using System.Management.Automation;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class CmdletBaseTests
{
    private class TestCmdlet : CmdletBase
    {
        public bool BeginCalled { get; private set; }
        public bool ProcessCalled { get; private set; }
        public bool EndCalled { get; private set; }

        protected override void Begin()
        {
            BeginCalled = true;
        }

        protected override void Process()
        {
            ProcessCalled = true;
        }

        protected override void End()
        {
            EndCalled = true;
        }
    }

    [Fact]
    public void Constructor_SetsCmdletContext()
    {
        // Arrange
        CmdletContext.Current = null;

        // Act
        var cmdlet = new TestCmdlet();

        // Assert
        Assert.Same(cmdlet, CmdletContext.Current);
    }

    [Fact]
    public void CmdletName_ReturnsTypeName()
    {
        // Arrange
        var cmdlet = new TestCmdlet();

        // Act
        var name = cmdlet.CmdletName;

        // Assert
        Assert.Equal("TestCmdlet", name);
    }

    [Fact]
    public void BeginProcessing_SetsCmdletContext()
    {
        // Arrange
        CmdletContext.Current = null;
        var cmdlet = new TestCmdlet();

        // Act
        cmdlet.BeginProcessing();

        // Assert
        Assert.Same(cmdlet, CmdletContext.Current);
        Assert.True(cmdlet.BeginCalled);
    }

    [Fact]
    public void ProcessRecord_CallsProcess()
    {
        // Arrange
        var cmdlet = new TestCmdlet();
        cmdlet.BeginProcessing();

        // Act
        cmdlet.ProcessRecord();

        // Assert
        Assert.True(cmdlet.ProcessCalled);
    }

    [Fact]
    public void EndProcessing_ClearsCmdletContext()
    {
        // Arrange
        var cmdlet = new TestCmdlet();
        cmdlet.BeginProcessing();
        cmdlet.ProcessRecord();

        // Act
        cmdlet.EndProcessing();

        // Assert
        Assert.Null(CmdletContext.Current);
        Assert.True(cmdlet.EndCalled);
    }

    [Fact]
    public void Logger_ReturnsLoggerInstance()
    {
        // Arrange
        ModuleServices.EnsureInitialized();
        var cmdlet = new TestCmdlet();

        // Act
        var logger = cmdlet.Logger;

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void Debugger_ReturnsDebuggerInstance()
    {
        // Arrange
        ModuleServices.EnsureInitialized();
        var cmdlet = new TestCmdlet();

        // Act
        var debugger = cmdlet.Debugger;

        // Assert
        Assert.NotNull(debugger);
    }
}
