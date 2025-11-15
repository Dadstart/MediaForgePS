using System.Net;
using System.Net.Http;
using Moq;
using Moq.Protected;
using Dadstart.Labs.MediaForgePS.Models;
using Dadstart.Labs.MediaForgePS.Services;

namespace Dadstart.Labs.MediaForgePS.Tests.Services;

public class HolidayScraperTests
{
    [Fact]
    public async Task GetHolidaysAsync_WithValidHtml_ReturnsHolidays()
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
                        <tr>
                            <td>2024-01-15</td>
                            <td>Martin Luther King Jr. Day</td>
                            <td>Federal Holiday</td>
                            <td>Honors civil rights leader</td>
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
        var date = new DateTime(2024, 1, 1);

        // Act
        var holidays = await scraper.GetHolidaysAsync(date);

        // Assert
        Assert.NotNull(holidays);
        Assert.NotEmpty(holidays);
        Assert.Contains(holidays, h => h.Name.Contains("New Year"));
    }

    [Fact]
    public async Task GetHolidaysAsync_WithEmptyHtml_ReturnsEmptyList()
    {
        // Arrange
        var html = "<html><body></body></html>";

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
        var date = new DateTime(2024, 1, 1);

        // Act
        var holidays = await scraper.GetHolidaysAsync(date);

        // Assert
        Assert.NotNull(holidays);
        Assert.Empty(holidays);
    }

    [Fact]
    public async Task GetHolidaysAsync_WithNetworkFailure_ThrowsHttpRequestException()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var scraper = new HolidayScraper(httpClient);
        var date = new DateTime(2024, 1, 1);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => scraper.GetHolidaysAsync(date));
    }

    [Fact]
    public async Task GetHolidaysAsync_WithMalformedHtml_ReturnsEmptyOrPartialResults()
    {
        // Arrange
        var html = "<html><body><table><tr><td>Invalid</td></tr></table></body></html>";

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
        var date = new DateTime(2024, 1, 1);

        // Act
        var holidays = await scraper.GetHolidaysAsync(date);

        // Assert
        Assert.NotNull(holidays);
        // Should handle malformed HTML gracefully without throwing
    }
}


