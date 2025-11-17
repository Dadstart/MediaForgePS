using System.Management.Automation;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Dadstart.Labs.MediaForgePS.Cmdlets;
using Dadstart.Labs.MediaForgePS.Models;
using Dadstart.Labs.MediaForgePS.Services;

namespace Dadstart.Labs.MediaForgePS.Tests.Cmdlets;

public class GetHolidayCommandTests : IDisposable
{
    public GetHolidayCommandTests()
    {
        // Reset service provider before each test
        ServiceProviderFactory.Reset();
    }

    [Fact]
    public void Cmdlet_DummyTest()
    {
        // Arrange & Act
        var cmdlet = new GetHolidayCommand
        {
            Date = new DateTime(2001, 1, 1)
        };

        // Assert
        Assert.NotNull(cmdlet);
        Assert.Equal(new DateTime(2001, 1, 1), cmdlet.Date);
    }

    public void Dispose()
    {
        // Clean up after each test
        ServiceProviderFactory.Reset();
    }
}
