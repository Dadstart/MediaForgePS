using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class SubtractNumberCommandTests : IDisposable
{
    private readonly PowerShell _powerShell;

    public SubtractNumberCommandTests()
    {
        _powerShell = PowerShell.Create();
        var assemblyPath = typeof(SubtractNumberCommand).Assembly.Location;
        _powerShell.AddCommand("Import-Module").AddArgument(assemblyPath).Invoke();
        _powerShell.Commands.Clear();
    }

    [Fact]
    public void ProcessRecord_WithValidNumbers_ReturnsDifference()
    {
        // Arrange & Act
        _powerShell.AddCommand("Subtract-Number")
            .AddParameter("Minuend", 10.0)
            .AddParameter("Subtrahend", 3.0);
        var results = _powerShell.Invoke();

        // Assert
        Assert.Single(results);
        Assert.Equal(7.0, (double)results[0].BaseObject);
    }

    [Fact]
    public void ProcessRecord_WithNegativeResult_ReturnsCorrectDifference()
    {
        // Arrange & Act
        _powerShell.AddCommand("Subtract-Number")
            .AddParameter("Minuend", 5.0)
            .AddParameter("Subtrahend", 10.0);
        var results = _powerShell.Invoke();

        // Assert
        Assert.Single(results);
        Assert.Equal(-5.0, (double)results[0].BaseObject);
    }

    [Fact]
    public void ProcessRecord_WithZero_ReturnsMinuend()
    {
        // Arrange & Act
        _powerShell.AddCommand("Subtract-Number")
            .AddParameter("Minuend", 10.0)
            .AddParameter("Subtrahend", 0.0);
        var results = _powerShell.Invoke();

        // Assert
        Assert.Single(results);
        Assert.Equal(10.0, (double)results[0].BaseObject);
    }

    [Fact]
    public void ProcessRecord_WithDecimalNumbers_ReturnsCorrectDifference()
    {
        // Arrange & Act
        _powerShell.AddCommand("Subtract-Number")
            .AddParameter("Minuend", 10.5)
            .AddParameter("Subtrahend", 3.7);
        var results = _powerShell.Invoke();

        // Assert
        Assert.Single(results);
        Assert.Equal(6.8, (double)results[0].BaseObject, 10);
    }

    public void Dispose()
    {
        _powerShell?.Dispose();
    }
}
