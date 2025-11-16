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

    public void Dispose()
    {
        // Clean up after each test
        ServiceProviderFactory.Reset();
    }

    [Fact]
    public void Cmdlet_WithSingleDate_CanBeInstantiated()
    {
        // Arrange & Act
        var cmdlet = new GetHolidayCommand
        {
            Date = new DateTime(2024, 1, 1)
        };

        // Assert
        Assert.NotNull(cmdlet);
        Assert.Equal(new DateTime(2024, 1, 1), cmdlet.Date);
    }

    [Fact]
    public void Cmdlet_WithDateRange_CanBeInstantiated()
    {
        // Arrange & Act
        var cmdlet = new GetHolidayCommand
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 1, 3)
        };

        // Assert
        Assert.NotNull(cmdlet);
        Assert.Equal(new DateTime(2024, 1, 1), cmdlet.StartDate);
        Assert.Equal(new DateTime(2024, 1, 3), cmdlet.EndDate);
    }

    [Fact]
    public void Cmdlet_Properties_CanBeSet()
    {
        // Arrange
        var cmdlet = new GetHolidayCommand();

        // Act
        cmdlet.Date = new DateTime(2024, 7, 4);
        cmdlet.StartDate = new DateTime(2024, 1, 1);
        cmdlet.EndDate = new DateTime(2024, 12, 31);

        // Assert
        Assert.Equal(new DateTime(2024, 7, 4), cmdlet.Date);
        Assert.Equal(new DateTime(2024, 1, 1), cmdlet.StartDate);
        Assert.Equal(new DateTime(2024, 12, 31), cmdlet.EndDate);
    }

    [Fact]
    public void ProcessRecord_WithSingleDate_UsesDependencyInjection()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <table class=""table table--left table--inner-borders-rows table--striped"">
                        <tr>
                            <td>2024-01-01</td>
                            <td>New Year's Day</td>
                            <td>Federal Holiday</td>
                            <td>First day of the year</td>
                            <td>All states</td>
                        </tr>
                    </table>
                </body>
            </html>";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(html)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var scraper = new HolidayScraper(httpClient);

        var services = new ServiceCollection();
        services.AddSingleton<HolidayScraper>(scraper);
        var serviceProvider = services.BuildServiceProvider();
        ServiceProviderFactory.SetServiceProvider(serviceProvider);

        var cmdlet = new GetHolidayCommand
        {
            Date = new DateTime(2024, 1, 1)
        };

        var output = new List<object>();
        cmdlet.CommandRuntime = new TestCommandRuntime(output);

        // Act
        cmdlet.ProcessRecord();

        // Assert
        Assert.Single(output);
        var holiday = Assert.IsType<Holiday>(output[0]);
        Assert.Contains("New Year", holiday.Name);
    }

    [Fact]
    public void ProcessRecord_WithInvalidDateRange_ReportsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<HolidayScraper>();
        var serviceProvider = services.BuildServiceProvider();
        ServiceProviderFactory.SetServiceProvider(serviceProvider);

        var cmdlet = new GetHolidayCommand
        {
            StartDate = new DateTime(2024, 1, 3),
            EndDate = new DateTime(2024, 1, 1)
        };

        var errors = new List<ErrorRecord>();
        cmdlet.CommandRuntime = new TestCommandRuntime(errors: errors);

        // Act
        cmdlet.ProcessRecord();

        // Assert
        Assert.Single(errors);
        Assert.Equal("InvalidDateRange", errors[0].FullyQualifiedErrorId);
    }

    private class TestCommandRuntime : ICommandRuntime
    {
        private readonly List<object> _output;
        private readonly List<ErrorRecord> _errors;

        public TestCommandRuntime(List<object>? output = null, List<ErrorRecord>? errors = null)
        {
            _output = output ?? new List<object>();
            _errors = errors ?? new List<ErrorRecord>();
        }

        public PSTransactionContext CurrentPSTransaction => throw new NotImplementedException();
        public bool ShouldProcess(string target) => true;
        public bool ShouldProcess(string target, string action) => true;
        public bool ShouldProcess(string verboseDescription, string verboseWarning, string caption) => true;
        public bool ShouldProcess(string verboseDescription, string verboseWarning, string caption, ref bool yesToAll, ref bool noToAll) => true;
        public bool ShouldContinue(string query, string caption) => true;
        public bool ShouldContinue(string query, string caption, ref bool yesToAll, ref bool noToAll) => true;
        public bool TransactionAvailable() => false;
        public void ThrowTerminatingError(ErrorRecord errorRecord) => throw new Exception(errorRecord.ToString());
        public void WriteObject(object sendToPipeline) => _output.Add(sendToPipeline);
        public void WriteObject(object sendToPipeline, bool enumerateCollection) => WriteObject(sendToPipeline);
        public void WriteError(ErrorRecord errorRecord) => _errors.Add(errorRecord);
        public void WriteVerbose(string message) { }
        public void WriteDebug(string message) { }
        public void WriteWarning(string message) { }
        public void WriteInformation(InformationRecord informationRecord) { }
        public void WriteProgress(ProgressRecord progressRecord) { }
        public void WriteCommandDetail(string text) { }
        public bool ShouldContinue(string query, string caption, bool hasSecurityImpact, ref bool yesToAll, ref bool noToAll) => true;
        public Host.PSHost Host => throw new NotImplementedException();
    }
}

