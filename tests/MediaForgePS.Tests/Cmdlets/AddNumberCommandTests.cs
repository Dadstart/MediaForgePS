using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class AddNumberCommandTests : IDisposable
{
    private readonly PowerShell _powerShell;

    public AddNumberCommandTests()
    {
        _powerShell = PowerShell.Create();
        var assemblyPath = typeof(AddNumberCommand).Assembly.Location;
        _powerShell.AddCommand("Import-Module").AddArgument(assemblyPath).Invoke();
        _powerShell.Commands.Clear();
    }

    [Fact]
    public void ProcessRecord_WithValidNumbers_ReturnsSum()
    {
        // Arrange & Act
        _powerShell.AddCommand("Add-Number")
            .AddParameter("FirstNumber", 5.0)
            .AddParameter("SecondNumber", 3.0);
        var results = _powerShell.Invoke();

        // Assert
        Assert.Single(results);
        Assert.Equal(8.0, (double)results[0].BaseObject);
    }

    [Fact]
    public void ProcessRecord_WithNegativeNumbers_ReturnsCorrectSum()
    {
        // Arrange & Act
        _powerShell.AddCommand("Add-Number")
            .AddParameter("FirstNumber", -5.0)
            .AddParameter("SecondNumber", -3.0);
        var results = _powerShell.Invoke();

        // Assert
        Assert.Single(results);
        Assert.Equal(-8.0, (double)results[0].BaseObject);
    }

    [Fact]
    public void ProcessRecord_WithZero_ReturnsOtherNumber()
    {
        // Arrange & Act
        _powerShell.AddCommand("Add-Number")
            .AddParameter("FirstNumber", 10.0)
            .AddParameter("SecondNumber", 0.0);
        var results = _powerShell.Invoke();

        // Assert
        Assert.Single(results);
        Assert.Equal(10.0, (double)results[0].BaseObject);
    }

    [Fact]
    public void ProcessRecord_WithDecimalNumbers_ReturnsCorrectSum()
    {
        // Arrange & Act
        _powerShell.AddCommand("Add-Number")
            .AddParameter("FirstNumber", 5.5)
            .AddParameter("SecondNumber", 3.7);
        var results = _powerShell.Invoke();

        // Assert
        Assert.Single(results);
        Assert.Equal(9.2, (double)results[0].BaseObject);
    }

    public void Dispose()
    {
        _powerShell?.Dispose();
    }
}
